using Godot;
using System;
using System.Collections.Generic;

namespace WorldEdit.Assets;

/// <summary>
/// 地表贴图集，按 HiveWE 的方式切块：
/// 一张地表 BLP 是 4 行 × 4（或 8）列的 64×64 小块，切成 16/32 个 layer 放进 texture array，
/// 每层单独生成 mipmap。这样每个 tile 采样时 UV 落在 0..1，不会在 mip 里和隔壁小块串色。
/// 第 4 个通道是 War3 的混合遮罩（alphaDepth=8），用于 tile 内多层地表叠加。
/// </summary>
public sealed class GroundTextureArray
{
    public const int LayersPerTile = 32;

    public Texture2DArray Texture { get; private set; }

    /// <summary>tileID → 该地表在数组里的首层（+0..31 是 32 个变体）。</summary>
    public Dictionary<string, int> BaseLayer { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static GroundTextureArray Build(TerrainTileSet tiles, IReadOnlyList<string> tileIds, List<string> used)
    {
        var array = new GroundTextureArray();
        var images = new Godot.Collections.Array<Image>();
        int tileSize = 0;

        foreach (string id in tileIds)
        {
            Image source = tiles.Get(id);
            if (source == null)
            {
                continue;
            }

            // 地表贴图有两种排版：512×256 = 8 列（32 个变体），256×256 = 4 列（16 个变体）。
            // 小块都是 64×64，所以两种都能进同一个 texture array，只是变体数不同。
            int size = source.GetHeight() / 4;
            int columns = size > 0 ? source.GetWidth() / size : 0;
            if (size <= 0 || columns < 4)
            {
                GD.PushWarning($"[TEX] 地表贴图布局异常，跳过 {id}: {source.GetWidth()}x{source.GetHeight()}");
                continue;
            }
            if (tileSize == 0)
            {
                tileSize = size;
            }
            if (size != tileSize || source.GetHeight() % 4 != 0)
            {
                GD.PushWarning($"[TEX] 瓦片尺寸不一致，跳过 {id}: {size}x{size} vs {tileSize}x{tileSize}");
                continue;
            }

            if (images.Count == 0)
            {
                // 第 0 层固定是「全透明黑」，给「这一格只有 1~3 种地表」时填空用。
                // HiveWE 的叠加式子是 color = color * color.a + next * (1 - color.a)，
                // 用不到的层指向它的 17 号黑透明纹理，等于把当前颜色再乘一次自己的遮罩 alpha。
                // War3 那种斑驳的过渡就是这么压出来的；如果拿底层重复填充，alpha 会被抵消，
                // 草地会比 HiveWE 亮一大截（实测 114 vs 68）。
                tileSize = size;
                Image blankCell = Image.CreateEmpty(tileSize, tileSize, false, Image.Format.Rgba8);
                blankCell.Fill(new Color(0f, 0f, 0f, 0f));
                // 必须和其他层一样带 mipmap，否则 CreateFromImages 尺寸不一致会整张失败（画面上变成品红棋盘格）
                blankCell.GenerateMipmaps();
                images.Add(blankCell);
            }

            int baseLayer = images.Count;
            for (int i = 0; i < LayersPerTile; i++)
            {
                // 左半是带暗纹的旧半区（变体 0-15），右半才是干净的那一套（变体 16-31）；
                // 只有 4 列的贴图没有右半，两边都用同一批小块。
                int cell = i % 16;
                int cx = cell % 4;
                int cy = cell / 4;
                if (columns >= 8 && i >= 16)
                {
                    cx += 4;
                }
                Image cellImage = Slice(source, cx * tileSize, cy * tileSize, tileSize);
                cellImage.GenerateMipmaps();
                images.Add(cellImage);
            }

            array.BaseLayer[id] = baseLayer;
            used.Add(id);
        }

        if (images.Count <= 1)
        {
            return null;
        }

        array.Texture = new Texture2DArray();
        Error error = array.Texture.CreateFromImages(images);
        if (error != Error.Ok)
        {
            GD.PushError($"[TEX] Texture2DArray 创建失败: {error}（{images.Count} 层，{tileSize}x{tileSize}）");
            return null;
        }
        return array;
    }

    /// <summary>
    /// w3e 的 variation（0-31）映射到数组层号。和 HiveWE 的 get_tile_variation 一致：
    /// 0-15 用右半区，16 用 15 号，更大的一律用 0 号。
    /// </summary>
    public static int VariationLayer(int variation)
    {
        if (variation <= 15)
        {
            return 16 + variation;
        }
        return variation == 16 ? 15 : 0;
    }

    public int LayerOf(string tileId, int variation)
    {
        if (!BaseLayer.TryGetValue(tileId, out int baseLayer))
        {
            return 0;
        }
        return baseLayer + VariationLayer(variation);
    }

    /// <summary>「没用到的层」用第 0 层（全透明黑）填，和 HiveWE 的 17 号黑透明纹理等价。</summary>
    public const int BlankLayer = 0;

    private static Image Slice(Image source, int x, int y, int size)
    {
        var cell = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        cell.BlitRect(source, new Rect2I(x, y, size, size), Vector2I.Zero);
        return cell;
    }
}
