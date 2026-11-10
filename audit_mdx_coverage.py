import os
import re
import sys
from collections import Counter, defaultdict

# 用法:
#   1) 客户端导出全量清单: OpenWC3.Game --mdx-inventory test/war3-unpack > inventory.txt
#   2) python3 audit_mdx_coverage.py [示例条数]
ROOT = os.path.dirname(os.path.abspath(__file__))
INVENTORY = os.path.join(ROOT, 'inventory.txt')
EXTRACT = os.path.join(ROOT, 'Extract', 'extract')
LIMIT = int(sys.argv[1]) if len(sys.argv) > 1 else 5


def read(path):
    try:
        return open(path, encoding='utf-8', errors='ignore').read()
    except OSError:
        return ''


def parse_inventory():
    models = []
    for line in open(INVENTORY, encoding='utf-8'):
        m = re.match(r'\[inv\] (\S+\.mdx) (.*)', line.strip())
        if not m:
            continue
        data = {'path': m.group(1)}
        for key, value in re.findall(r'(\w+)=([\w,:.]+)', m.group(2)):
            data[key] = value
        models.append(data)
    return models


def num(data, key):
    try:
        return int(data.get(key, 0))
    except (TypeError, ValueError):
        return 0


def parse_tracks(text):
    """返回 [(node_path, prop, key_count)]"""
    entries = []
    paths = [(m.start(), m.group(1), m.group(2))
             for m in re.finditer(r'tracks/(\d+)/path = NodePath\("([^"]+)"\)', text)]
    for index, (start, _, path) in enumerate(paths):
        end = paths[index + 1][0] if index + 1 < len(paths) else len(text)
        block = text[start:end]
        node, _, prop = path.rpartition(':')
        keys = re.search(r'keys = PackedFloat32Array\(([^)]*)\)', block)
        if keys:
            count = len(keys.group(1).split(',')) if keys.group(1).strip() else 0
        else:
            keys = re.search(r'"times": PackedFloat32Array\(([^)]*)\)', block)
            count = len(keys.group(1).split(',')) if keys and keys.group(1).strip() else 0
        entries.append((node, prop, count))
    return entries


def main():
    models = parse_inventory()
    stats = Counter()
    issues = defaultdict(list)

    for data in models:
        rel = data['path'][:-4]
        out_dir = os.path.join(EXTRACT, os.path.dirname(rel))
        name = os.path.basename(rel)
        scene = read(os.path.join(out_dir, name + '.tscn'))
        if not scene:
            stats['scene-missing'] += 1
            issues['scene-missing'].append(rel)
            continue
        stats['models'] += 1

        clip_files = sorted(f for f in os.listdir(out_dir)
                            if re.match(re.escape(name) + r'\.\d+_.*\.tres$', f))
        fx_scenes = [f for f in os.listdir(out_dir) if re.match(re.escape(name) + r'\.fx\d+\.tscn$', f)]
        fx_materials = [f for f in os.listdir(out_dir) if re.match(re.escape(name) + r'\.fx\d+\.tres$', f)]
        fx_scene_text = ''.join(read(os.path.join(out_dir, f)) for f in fx_scenes)
        fx_material_text = ''.join(read(os.path.join(out_dir, f)) for f in fx_materials)

        if len(clip_files) != num(data, 'seq'):
            stats['clip-count-mismatch'] += 1
            issues['clip-count-mismatch'].append(
                f'{rel} seq={num(data, "seq")} clips={len(clip_files)}')

        bone_ids = set()
        dense_bone = 0
        geoset_visible = 0
        emitter_channels = defaultdict(set)
        for clip in clip_files:
            for node, prop, keys in parse_tracks(read(os.path.join(out_dir, clip))):
                if re.fullmatch(r'node\d+', prop):
                    bone_ids.add(int(prop[4:]))
                    if keys > 40:
                        dense_bone += 1
                elif node.startswith('fx_node'):
                    emitter_channels[int(node[7:].split('/')[0])].add(prop)
                elif 'geoset' in node and prop == 'visible':
                    geoset_visible += 1

        if num(data, 'bonesInClip') and len(bone_ids) < num(data, 'bonesInClip'):
            stats['bone-track-missing'] += 1
            issues['bone-track-missing'].append(
                f'{rel} inClip={num(data, "bonesInClip")} exported={len(bone_ids)}')

        if num(data, 'bonesGlobal') and dense_bone == 0:
            stats['global-bone-not-resampled'] += 1
            issues['global-bone-not-resampled'].append(f'{rel} bonesGlobal={num(data, "bonesGlobal")}')

        if num(data, 'emit') != len(re.findall(r'name="fx_node\d+"', scene)):
            stats['emitter-count-mismatch'] += 1
            issues['emitter-count-mismatch'].append(
                f'{rel} mdx={num(data, "emit")} scene={len(re.findall(r"name=.fx_node", scene))}')

        if num(data, 'squirt') > fx_scene_text.count('one_shot = true'):
            stats['squirt-one-shot-missing'] += 1
            issues['squirt-one-shot-missing'].append(
                f'{rel} squirt={num(data, "squirt")} one_shot={fx_scene_text.count("one_shot = true")}')

        atlas = len(re.findall(r'atlas_columns_rows = Vector2\((?!1, 1\))', fx_material_text))
        if num(data, 'atlas') > atlas:
            stats['atlas-missing'] += 1
            issues['atlas-missing'].append(f'{rel} atlas={num(data, "atlas")} exported={atlas}')

        tail = len(re.findall(r'metadata/war3_head_or_tail = [12]', fx_scene_text))
        if num(data, 'tail') > tail:
            stats['tail-missing'] += 1
            issues['tail-missing'].append(f'{rel} tail={num(data, "tail")} exported={tail}')

        if num(data, 'rib') != len(re.findall(r'name="rib_node', scene)):
            stats['ribbon-count-mismatch'] += 1
            issues['ribbon-count-mismatch'].append(
                f'{rel} mdx={num(data, "rib")} scene={len(re.findall(r"name=.rib_node", scene))}')

        if num(data, 'lite') != len(re.findall(r'name="light_node', scene)):
            stats['light-count-mismatch'] += 1
            issues['light-count-mismatch'].append(
                f'{rel} mdx={num(data, "lite")} scene={len(re.findall(r"name=.light_node", scene))}')

        if num(data, 'geoAlpha') and geoset_visible == 0:
            stats['geoset-visibility-missing'] += 1
            issues['geoset-visibility-missing'].append(f'{rel} geoAlpha={num(data, "geoAlpha")}')

        materials = ''.join(read(os.path.join(out_dir, f)) for f in os.listdir(out_dir)
                            if re.match(re.escape(name) + r'\.mat\d+\.tres$', f))
        if num(data, 'team') and 'use_team_color = true' not in materials:
            stats['team-color-missing'] += 1
            issues['team-color-missing'].append(f'{rel} team={num(data, "team")}')
        if num(data, 'twoLayer') and 'detail_texture' not in materials:
            stats['detail-layer-missing'] += 1
            issues['detail-layer-missing'].append(f'{rel} twoLayer={num(data, "twoLayer")}')
        if num(data, 'alphaLayers') and 'layer_alpha' not in materials:
            stats['layer-alpha-missing'] += 1
            issues['layer-alpha-missing'].append(f'{rel} alphaLayers={num(data, "alphaLayers")}')

        if num(data, 'geoColor'):
            tints = sum(read(os.path.join(out_dir, clip)).count('shader_parameter/geoset_tint') for clip in clip_files)
            if tints == 0:
                stats['geoset-tint-missing'] += 1
                issues['geoset-tint-missing'].append(f'{rel} geoColor={num(data, "geoColor")}')
        if num(data, 'texAnimLayers') and 'uv_offset' not in materials:
            stats['uv-animation-missing'] += 1
            issues['uv-animation-missing'].append(f'{rel} texAnimLayers={num(data, "texAnimLayers")}')
        if num(data, 'cams'):
            stats['camera-not-exported'] += 1
            issues['camera-not-exported'].append(f'{rel} cams={num(data, "cams")}')
        if num(data, 'evts'):
            sidecar = read(os.path.join(out_dir, name + '.wc3.json'))
            if '\"frames\":' not in sidecar and '"frames":' not in sidecar:
                stats['event-frames-missing'] += 1
                issues['event-frames-missing'].append(f'{rel} evts={num(data, "evts")}')

    print('=== 覆盖统计 ===')
    for key, count in sorted(stats.items(), key=lambda item: -item[1]):
        print(f'  {key:34s} {count}')
    print()
    for key, items in sorted(issues.items(), key=lambda item: -len(item[1])):
        print(f'--- {key} ({len(items)}) ---')
        for sample in items[:LIMIT]:
            print('   ', sample)


main()