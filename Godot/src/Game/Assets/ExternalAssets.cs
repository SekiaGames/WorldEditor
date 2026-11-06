using Godot;
using System;
using System.IO;

namespace WorldEdit.Assets;

/// <summary>
/// 资源外置。exe 里只装代码和不属于 converted 的少量资源，
/// assets/converted 整棵打成 exe 同目录的 converted.vN.pck，启动时挂上来。
/// 包名里的 N 就是 AssetStamp.Version：exe 只认自己版本号的包，版本对不上就是没资源，
/// 不回退到别的名字——省得跑起来才发现用的是旧资源。
/// 若某次导出把 converted 也内嵌了，这里检测到 res://assets/converted 已存在就直接返回。
/// </summary>
public static class ExternalAssets
{
    /// <summary>按当前版本号算出包名，和 export.bat 的命名规则一致。</summary>
    public static string PackNameFor(int version)
    {
        return $"converted.v{version}.pck";
    }

    public static void Mount()
    {
        if (DirAccess.DirExistsAbsolute("res://assets/converted"))
        {
            return;
        }

        string directory = Path.GetDirectoryName(OS.GetExecutablePath());
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return;
        }

        string name = PackNameFor(Core.Tools.AssetStamp.Version);
        string pack = Path.Combine(directory, name);
        if (!File.Exists(pack))
        {
            GD.PushError($"[ASSETS] 资源没有内嵌，也找不到 {name}（解压版本 v{Core.Tools.AssetStamp.Version}）："
                + $"{directory}。先用 export.bat 打包。");
            return;
        }

        // 不覆盖 exe 内嵌的内容：内嵌的是刚编译的程序集，覆盖了就等于跑旧代码
        bool ok = ProjectSettings.LoadResourcePack(pack, false);
        GD.Print($"[ASSETS] 挂载 {name} -> {ok}");
    }
}
