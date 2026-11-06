using Godot;
using System;

namespace WorldEdit.Core.Tools;

/// <summary>
/// 命令行里的路径解析。Godot 的工作目录是工程目录，而截图、产物一般写在仓库根的 test/ 下，
/// 所以输入路径要按「当前目录 → 工程目录 → 仓库根」依次找，输出路径一律落在仓库根。
/// </summary>
internal static class CliPath
{
    public static string Resolve(string path)
    {
        if (System.IO.Path.IsPathRooted(path))
        {
            return path;
        }

        string direct = System.IO.Path.GetFullPath(path);
        if (System.IO.File.Exists(direct) || System.IO.Directory.Exists(direct))
        {
            return direct;
        }

        string project = ProjectSettings.GlobalizePath("res://");
        string inProject = System.IO.Path.GetFullPath(System.IO.Path.Combine(project, path));
        if (System.IO.File.Exists(inProject) || System.IO.Directory.Exists(inProject))
        {
            return inProject;
        }

        string inRepository = System.IO.Path.GetFullPath(System.IO.Path.Combine(project, "..", path));
        if (System.IO.File.Exists(inRepository) || System.IO.Directory.Exists(inRepository))
        {
            return inRepository;
        }
        return direct;
    }

    public static string ResolveOutput(string path)
    {
        if (System.IO.Path.IsPathRooted(path))
        {
            return path;
        }
        string project = ProjectSettings.GlobalizePath("res://");
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(project, "..", path));
    }

    /// <summary>写文件前先建目录。</summary>
    public static void EnsureDirectory(string filePath)
    {
        DirAccess.MakeDirRecursiveAbsolute(System.IO.Path.GetDirectoryName(filePath));
    }
}
