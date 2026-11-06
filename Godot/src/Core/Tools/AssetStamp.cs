using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WorldEdit.Core.Tools;

/// <summary>
/// 解压产物的版本标记。extract 脚本跑完在输出目录写 _extract-version.txt，
/// 下次运行先比对版本，一致就整轮跳过。
/// 改了转换结果（glb 结构、材质、动画、图片格式、新增资产类别）就把 Version 加一。
/// </summary>
public static class AssetStamp
{
    /// <summary>产物版本。产物与旧版不兼容时加一，解压脚本据此决定要不要重跑。</summary>
    public const int Version = 1;

    public const string FileName = "_extract-version.txt";

    /// <summary>判定产物完整的必要条件：这几类资产目录都得在。</summary>
    private static readonly string[] RequiredDirectories = { "Units", "Textures", "Tables", "Texts" };

    public static string PathOf(string root)
    {
        return System.IO.Path.Combine(root, FileName);
    }

    /// <summary>
    /// --asset-status=目录
    /// 打印产物版本状态，最后一行固定是 STATE=FRESH / STALE / MISSING / INCOMPLETE，
    /// 解压脚本用它判断能不能跳过。
    /// </summary>
    public static void PrintStatus(string directory)
    {
        string root = CliPath.Resolve(directory);
        Console.WriteLine($"[STAMP] 目录 {root}");
        Dictionary<string, string> values = Read(PathOf(root));
        if (values.Count == 0)
        {
            Console.WriteLine($"[STAMP] 没有版本标记 {FileName}，需要整轮解压");
            Console.WriteLine("STATE=MISSING");
            return;
        }

        values.TryGetValue("version", out string rawVersion);
        values.TryGetValue("time", out string time);
        values.TryGetValue("source", out string source);
        values.TryGetValue("models", out string models);
        int.TryParse(rawVersion, out int found);

        Console.WriteLine($"[STAMP] 标记版本 {rawVersion}，当前版本 {Version}，"
            + $"解压时间 {time}，源 {source}，模型 {models} 个");
        if (found != Version)
        {
            Console.WriteLine("[STAMP] 版本不一致，产物需要重新生成");
            Console.WriteLine("STATE=STALE");
            return;
        }

        var missing = new List<string>();
        foreach (string name in RequiredDirectories)
        {
            if (!Directory.Exists(System.IO.Path.Combine(root, name)))
            {
                missing.Add(name);
            }
        }
        if (missing.Count > 0)
        {
            Console.WriteLine($"[STAMP] 产物不完整，缺少目录 {string.Join("、", missing)}");
            Console.WriteLine("STATE=INCOMPLETE");
            return;
        }

        Console.WriteLine("STATE=FRESH");
    }

    /// <summary>
    /// --asset-stamp=目录[|源目录]
    /// 只在整轮解压成功后调用，写下版本、时间、源目录和产物数量。
    /// </summary>
    public static void Write(string directory, string source)
    {
        string root = CliPath.Resolve(directory);
        string path = PathOf(root);
        try
        {
            Directory.CreateDirectory(root);
            int models = Count(root, "*.glb");
            int images = Count(root, "*.png");
            string time = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");
            var builder = new StringBuilder();
            builder.Append($"version={Version}\n");
            builder.Append($"time={time}\n");
            builder.Append($"source={source}\n");
            builder.Append($"models={models}\n");
            builder.Append($"images={images}\n");
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
            Console.WriteLine($"[STAMP] 已写版本标记 {path}");
            Console.WriteLine($"[STAMP] 版本 {Version}，时间 {time}，模型 {models}，图片 {images}");
        }
        catch (Exception exception)
        {
            GD.PushWarning($"[STAMP] 版本标记写入失败 {path}: {exception.Message}");
        }
    }

    // ---------------------------------------------------------------- 资源包版本

    /// <summary>
    /// --asset-version          打印 VERSION=N
    /// --asset-version=文件     只把数字写进文件，脚本用 set /p 直接读，
    /// 避免在 cmd 里解析带引号的命令行输出
    /// </summary>
    public static void PrintVersion(string targetFile = null)
    {
        if (!string.IsNullOrEmpty(targetFile))
        {
            try
            {
                string path = CliPath.ResolveOutput(targetFile);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                File.WriteAllText(path, Version.ToString(), new UTF8Encoding(false));
                return;
            }
            catch (Exception exception)
            {
                GD.PushWarning($"[STAMP] 版本号写入失败 {targetFile}: {exception.Message}");
            }
        }
        Console.WriteLine($"VERSION={Version}");
    }

    private static Dictionary<string, string> Read(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
        {
            return values;
        }
        foreach (string line in File.ReadAllLines(path))
        {
            int split = line.IndexOf('=');
            if (split <= 0)
            {
                continue;
            }
            values[line.Substring(0, split).Trim()] = line.Substring(split + 1).Trim();
        }
        return values;
    }

    private static int Count(string root, string pattern)
    {
        try
        {
            return Directory.GetFiles(root, pattern, SearchOption.AllDirectories).Length;
        }
        catch (Exception)
        {
            return 0;
        }
    }
}
