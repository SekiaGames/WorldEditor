using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using WorldEdit.Core.Formats.Mdx;

namespace WorldEdit.Core.Tools;

/// <summary>
/// MDX → glb 批量转换。
/// 对应 tools/mdx2glb.py，区别是源可以直接是游戏安装目录的 MPQ（不需要先解包）。
/// </summary>
public static class MdxExportCli
{
    private const string DefaultTarget = "Godot/assets/converted";
    private const string ConvertedRoot = "Godot/assets/converted";

    /// <summary>
    /// --mdx-export                      源 = 安装目录 MPQ，输出 = assets/converted
    /// --mdx-export=输出目录              同上，指定输出
    /// --mdx-export=源目录|输出目录        从解包目录读
    /// </summary>
    public static void Export(string sourceDirectory, string targetDirectory, string only = null)
    {
        string target = CliPath.ResolveOutput(string.IsNullOrEmpty(targetDirectory)
            ? DefaultTarget
            : targetDirectory);
        string convertedRoot = Path.GetFullPath(CliPath.ResolveOutput(ConvertedRoot));
        Directory.CreateDirectory(target);

        var entries = new List<(string Relative, Func<byte[]> Read)>();
        if (string.IsNullOrEmpty(sourceDirectory))
        {
            foreach (string name in War3Cli.AllNames(WorldEdit.Assets.War3Assets.GameRoot))
            {
                if (!name.EndsWith(".mdx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string relative = name.Replace('/', '\\').TrimStart('\\');
                entries.Add((relative, () => WorldEdit.Assets.War3Assets.Read(relative)));
            }
            entries.Sort((a, b) => string.CompareOrdinal(a.Relative, b.Relative));
            Console.WriteLine($"[MDX] 源 {WorldEdit.Assets.War3Assets.GameRoot} 的 MPQ，共 {entries.Count} 个 .mdx");
        }
        else
        {
            string source = CliPath.Resolve(sourceDirectory);
            if (!Directory.Exists(source))
            {
                throw new DirectoryNotFoundException($"源目录不存在: {source}");
            }
            var files = new List<string>(Directory.GetFiles(source, "*.mdx", SearchOption.AllDirectories));
            files.Sort(StringComparer.Ordinal);
            foreach (string file in files)
            {
                string relative = Path.GetRelativePath(source, file);
                entries.Add((relative, () => File.ReadAllBytes(file)));
            }
            Console.WriteLine($"[MDX] 源 {source}，共 {entries.Count} 个 .mdx");
        }

        if (!string.IsNullOrEmpty(only))
        {
            entries.RemoveAll(entry => entry.Relative.IndexOf(only, StringComparison.OrdinalIgnoreCase) < 0);
            Console.WriteLine($"[MDX] 只导匹配 \"{only}\" 的 {entries.Count} 个模型");
        }

        var failures = new List<string>();
        long totalSource = 0;
        long totalTarget = 0;
        int success = 0;

        for (int index = 0; index < entries.Count; index++)
        {
            (string relative, Func<byte[]> read) = entries[index];
            string output = Path.Combine(target, Path.ChangeExtension(relative, ".glb"));
            try
            {
                byte[] data = read();
                MdxDocument document = MdxDocument.Parse(data);
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                (byte[] blob, JsonObject _) = GlbWriter.Build(document, Path.GetDirectoryName(output), convertedRoot);
                File.WriteAllBytes(output, blob);
                // 材质 / 动画 / per-model 场景拆成独立 Godot 资源，glb 只留几何 + 骨骼 + 蒙皮
                ModelBundle.Write(document, output);
                File.WriteAllBytes(Path.ChangeExtension(output, ".wc3.json"),
                    new UTF8Encoding(false).GetBytes(Sidecar(document).ToJsonString(JsonOptions)));
                totalSource += data.Length;
                totalTarget += blob.Length;
                success++;
            }
            catch (Exception exception)
            {
                string trace = exception.StackTrace ?? "";
                string firstFrame = trace.Split('\n').Length > 0 ? trace.Split('\n')[0].Trim() : "";
                failures.Add($"{relative}\t{exception.GetType().Name}: {exception.Message} @ {firstFrame}");
            }

            if ((index + 1) % 100 == 0)
            {
                Console.WriteLine($"  {index + 1}/{entries.Count}  {relative}  "
                    + $"{totalTarget / 1048576.0:0.0} MB  失败 {failures.Count}");
            }
        }

        Console.WriteLine($"[MDX] 完成：成功 {success}，失败 {failures.Count}，"
            + $"{totalSource / 1048576.0:0.0} MB -> {totalTarget / 1048576.0:0.0} MB");
        Console.WriteLine($"[MDX] 目录 {target}");

        if (failures.Count > 0)
        {
            File.WriteAllText(Path.Combine(target, "_model-report.txt"),
                string.Join("\n", failures), new UTF8Encoding(false));
            foreach (string line in failures.GetRange(0, Math.Min(10, failures.Count)))
            {
                Console.WriteLine("  " + line);
            }
        }
    }

    /// <summary>
    /// --mdx-probe=相对路径
    /// 打印一个模型的贴图表与材质层，用来核对多层材质的取图顺序。
    /// </summary>
    public static void Probe(string relative)
    {
        byte[] data = WorldEdit.Assets.War3Assets.Read(relative);
        int mtls = IndexOfTag(data, "MTLS");
        if (mtls >= 0)
        {
            int window = Math.Min(160, data.Length - mtls);
            Console.WriteLine($"[MDX] MTLS @{mtls} 原始字节: {System.Convert.ToHexString(data, mtls, window)}");
        }
        MdxDocument document = MdxDocument.Parse(data);
        Console.WriteLine($"[MDX] {relative}: 几何 {document.Geosets.Count} 个、贴图 {document.Textures.Count} 张、材质 {document.Materials.Count} 个");
        for (int i = 0; i < document.Textures.Count; i++)
        {
            Console.WriteLine($"  贴图{i}: replaceable={document.Textures[i].ReplaceableId} path={document.Textures[i].Path}");
        }
        for (int i = 0; i < document.Materials.Count; i++)
        {
            MdxMaterial material = document.Materials[i];
            Console.WriteLine($"  材质{i}: {material.Layers.Count} 层");
            for (int j = 0; j < material.Layers.Count; j++)
            {
                MdxLayer layer = material.Layers[j];
                string path = layer.TextureId >= 0 && layer.TextureId < document.Textures.Count
                    ? document.Textures[layer.TextureId].Path
                    : "(越界)";
                Console.WriteLine($"    层{j}: filter={layer.FilterMode} texId={layer.TextureId} -> {path}");
            }
        }
    }

    private static int IndexOfTag(byte[] data, string tag)
    {
        for (int i = 0; i + 4 <= data.Length; i++)
        {
            if (data[i] == (byte)tag[0] && data[i + 1] == (byte)tag[1]
                && data[i + 2] == (byte)tag[2] && data[i + 3] == (byte)tag[3])
            {
                return i;
            }
        }
        return -1;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    private static JsonObject Sidecar(MdxDocument document)
    {
        var sequences = new JsonArray();
        foreach (MdxSequence sequence in document.Sequences)
        {
            sequences.Add(new JsonObject
            {
                ["name"] = sequence.Name,
                ["start"] = sequence.Start,
                ["end"] = sequence.End,
            });
        }

        var textures = new JsonArray();
        foreach (MdxTexture texture in document.Textures)
        {
            textures.Add(new JsonObject
            {
                ["replaceableId"] = texture.ReplaceableId,
                ["path"] = texture.Path,
            });
        }

        var materials = new JsonArray();
        foreach (MdxMaterial material in document.Materials)
        {
            var layers = new JsonArray();
            foreach (MdxLayer layer in material.Layers)
            {
                layers.Add(new JsonObject
                {
                    ["filterMode"] = layer.FilterMode,
                    ["shadingFlags"] = layer.ShadingFlags,
                    ["textureId"] = layer.TextureId,
                    ["textureAnimationId"] = layer.TextureAnimationId,
                    ["coordId"] = layer.CoordId,
                    ["alpha"] = layer.Alpha,
                });
            }
            materials.Add(new JsonObject
            {
                ["priority"] = material.Priority,
                ["flags"] = material.Flags,
                ["layers"] = layers,
            });
        }

        var attachments = new JsonArray();
        foreach (MdxNode node in document.Attachments)
        {
            attachments.Add(new JsonObject
            {
                ["path"] = node.AttachmentPath,
                ["attachmentId"] = node.AttachmentId,
                ["objectId"] = node.ObjectId,
                ["parentId"] = node.ParentId,
            });
        }

        var events = new JsonArray();
        foreach (MdxNode node in document.EventObjects)
        {
            events.Add(new JsonObject { ["objectId"] = node.ObjectId });
        }

        var collisions = new JsonObject();
        foreach (var pair in document.Collisions)
        {
            collisions[pair.Key] = pair.Value;
        }

        var globals = new JsonArray();
        foreach (uint value in document.GlobalSequences)
        {
            globals.Add(value);
        }

        var pivots = new JsonArray();
        for (int i = 0; i + 2 < document.Pivots.Length; i += 3)
        {
            pivots.Add(new JsonArray
            {
                document.Pivots[i], document.Pivots[i + 1], document.Pivots[i + 2],
            });
        }

        return new JsonObject
        {
            ["name"] = document.Name,
            ["sequences"] = sequences,
            ["textures"] = textures,
            ["materials"] = materials,
            ["attachments"] = attachments,
            ["eventObjects"] = events,
            ["collision"] = collisions,
            ["particles"] = document.Particles,
            ["ribbons"] = document.Ribbons,
            ["globalSequences"] = globals,
            ["pivots"] = pivots,
        };
    }
}
