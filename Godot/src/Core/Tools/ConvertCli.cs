using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WorldEdit.Core.Tools;

/// <summary>
/// 数据表转换：SLK / TXT → C# 源码。
/// 对应 tools/ 下的 slk2cs.py / txt2cs.py，区别是**直接从游戏安装目录的 MPQ 读**，
/// 不再需要先用 MPQ 工具解包一份目录树。
/// </summary>
public static class ConvertCli
{
    private const string DefaultTableTarget = "Godot/assets/converted/Tables";
    private const string DefaultTextTarget = "Godot/assets/converted/Texts";

    /// <summary>按扩展名从 MPQ 的 listfile 里挑出文件（返回反斜杠相对路径，已排序）。</summary>
    private static List<string> War3Files(string extension)
    {
        var names = new List<string>();
        foreach (string name in War3Cli.AllNames(WorldEdit.Assets.War3Assets.GameRoot))
        {
            if (!name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            names.Add(name.Replace('/', '\\').TrimStart('\\'));
        }
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private static byte[] ReadEntry(string relative)
    {
        return WorldEdit.Assets.War3Assets.Read(relative);
    }

    private static string CSharpLiteral(string text)
    {
        var builder = new StringBuilder(text.Length + 8);
        builder.Append('"');
        foreach (char ch in text)
        {
            switch (ch)
            {
                case '\\': builder.Append("\\\\"); break;
                case '"': builder.Append("\\\""); break;
                case '\r': break;
                case '\n': builder.Append("\\n"); break;
                case '\t': builder.Append("\\t"); break;
                default: builder.Append(ch); break;
            }
        }
        builder.Append('"');
        return builder.ToString();
    }

    private static void ClearGenerated(string target)
    {
        if (!Directory.Exists(target))
        {
            Directory.CreateDirectory(target);
            return;
        }
        foreach (string stale in Directory.GetFiles(target))
        {
            string name = Path.GetFileName(stale);
            if (name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".cs.uid", StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(stale);
            }
        }
    }

    private static void WriteText(string path, string text)
    {
        File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));
    }

    private static string TrimToName(string text, string fallback)
    {
        var builder = new StringBuilder();
        foreach (char ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
        }
        string name = builder.Length == 0 ? fallback : builder.ToString();
        return char.ToUpperInvariant(name[0]) + name[1..];
    }

    // ---------------------------------------------------------------- SLK

    /// <summary>按未加引号的分号切分。</summary>
    private static List<string> SplitFields(string line)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        bool quoted = false;
        for (int i = 2; i < line.Length; i++)
        {
            char ch = line[i];
            if (ch == '"')
            {
                quoted = !quoted;
                current.Append(ch);
            }
            else if (ch == ';' && !quoted)
            {
                parts.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }
        parts.Add(current.ToString());
        return parts;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1].Replace("\"\"", "\"");
        }
        return value;
    }

    private static (List<string> Columns, List<string[]> Rows) ParseSlk(byte[] data)
    {
        string text = Encoding.Latin1.GetString(data);
        var cells = new Dictionary<(int Row, int Column), string>();
        int row = 1;
        int column = 1;

        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.Length < 2 || line[0] != 'C' || line[1] != ';')
            {
                continue;
            }
            foreach (string field in SplitFields(line))
            {
                if (field.Length == 0)
                {
                    continue;
                }
                char kind = field[0];
                string body = field[1..];
                if (kind == 'X' && int.TryParse(body, out int x) && body.Length > 0)
                {
                    column = x;
                }
                else if (kind == 'Y' && int.TryParse(body, out int y) && body.Length > 0)
                {
                    row = y;
                }
                else if (kind == 'K')
                {
                    cells[(row, column)] = Unquote(body);
                }
            }
        }

        if (cells.Count == 0)
        {
            throw new InvalidDataException("空的 slk");
        }

        var rowsUsed = new SortedSet<int>();
        var columnsUsed = new SortedSet<int>();
        foreach (var key in cells.Keys)
        {
            rowsUsed.Add(key.Row);
            columnsUsed.Add(key.Column);
        }

        int firstRow = 0;
        foreach (int value in rowsUsed)
        {
            firstRow = value;
            break;
        }

        var columns = new List<string>();
        foreach (int c in columnsUsed)
        {
            columns.Add(cells.TryGetValue((firstRow, c), out string name) ? name : "");
        }

        var rows = new List<string[]>();
        bool skippedFirst = false;
        foreach (int r in rowsUsed)
        {
            if (!skippedFirst)
            {
                skippedFirst = true;
                continue;
            }
            var values = new string[columnsUsed.Count];
            int index = 0;
            foreach (int c in columnsUsed)
            {
                values[index++] = cells.TryGetValue((r, c), out string value) ? value : "";
            }
            rows.Add(values);
        }
        return (columns, rows);
    }

    private static string EmitTable(string tableName, List<string> columns, List<string[]> rows, string source)
    {
        var builder = new StringBuilder();
        builder.Append("// <auto-generated>\n");
        builder.Append($"// 由 Godot 的 --table-export 从 {source} 生成，不要手改。\n");
        builder.Append("// 重新生成：Godot.exe --path Godot --headless -- --table-export\n");
        builder.Append("// </auto-generated>\n\n");
        builder.Append("using WorldEdit.Core.Formats.Slk;\n\n");
        builder.Append("namespace WorldEdit.Assets.Generated;\n\n");
        builder.Append("public static partial class War3Tables\n{\n");
        builder.Append($"    // {source}：{rows.Count} 行 × {columns.Count} 列\n");
        builder.Append($"    private static SlkTable _cached{tableName};\n\n");

        builder.Append($"    private static readonly string[] {tableName}Columns =\n    [\n");
        for (int i = 0; i < columns.Count; i += 8)
        {
            int end = Math.Min(i + 8, columns.Count);
            builder.Append("        ");
            for (int j = i; j < end; j++)
            {
                if (j > i)
                {
                    builder.Append(", ");
                }
                builder.Append(CSharpLiteral(columns[j]));
            }
            builder.Append(",\n");
        }
        builder.Append("    ];\n\n");

        builder.Append($"    private static readonly string[][] {tableName}Rows =\n    [\n");
        foreach (string[] row in rows)
        {
            builder.Append("        [");
            for (int i = 0; i < row.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }
                builder.Append(CSharpLiteral(row[i]));
            }
            builder.Append("],\n");
        }
        builder.Append("    ];\n\n");

        builder.Append($"    public static SlkTable {tableName}() =>\n");
        builder.Append($"        _cached{tableName} ??= SlkTable.FromRows({tableName}Columns, {tableName}Rows);\n");
        builder.Append("}\n");
        return builder.ToString();
    }

    /// <summary>
    /// --table-export[=输出目录]
    /// 把 MPQ 里全部 .slk 转成 C#，一张表一个文件。
    /// </summary>
    public static void ExportTables(string targetDirectory)
    {
        string target = CliPath.ResolveOutput(string.IsNullOrEmpty(targetDirectory)
            ? DefaultTableTarget
            : targetDirectory);
        List<string> files = War3Files(".slk");
        Console.WriteLine($"[TABLE] 源 {WorldEdit.Assets.War3Assets.GameRoot} 的 MPQ，共 {files.Count} 张 .slk");

        ClearGenerated(target);
        var used = new HashSet<string>(StringComparer.Ordinal);
        long totalBytes = 0;
        long totalRows = 0;
        int written = 0;

        foreach (string relative in files)
        {
            string baseName = TrimToName(Path.GetFileNameWithoutExtension(relative), "Table");
            string name = baseName;
            if (!used.Add(name))
            {
                string directory = Path.GetFileName(Path.GetDirectoryName(relative) ?? "");
                name = TrimToName(directory + baseName, "Table");
                used.Add(name);
            }

            byte[] data;
            try
            {
                data = ReadEntry(relative);
            }
            catch (Exception e)
            {
                Console.WriteLine($"  [跳过] {relative}: {e.Message}");
                continue;
            }

            (List<string> columns, List<string[]> rows) = ParseSlk(data);
            string text = EmitTable(name, columns, rows, relative.Replace('\\', '/'));
            WriteText(Path.Combine(target, name + ".cs"), text);
            totalBytes += Encoding.UTF8.GetByteCount(text);
            totalRows += rows.Count;
            written++;
            Console.WriteLine($"  {relative,-44} {rows.Count,5} 行 × {columns.Count,3} 列 -> {name}.cs");
        }

        Console.WriteLine($"\n[TABLE] {written} 张表，{totalRows} 行数据，输出 {totalBytes / 1048576.0:0.0} MB");
        Console.WriteLine($"[TABLE] 目录 {target}");
    }

    // ---------------------------------------------------------------- TXT

    private static string DecodeText(byte[] raw)
    {
        try
        {
            var strict = new UTF8Encoding(false, true);
            string text = strict.GetString(raw);
            return text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;
        }
        catch (DecoderFallbackException)
        {
            // 非 UTF-8 的表按 Latin1 兜底，保证不丢字节（中文会显示为乱码，重跑时再补 GBK 支持）
            return Encoding.Latin1.GetString(raw);
        }
    }

    private static List<KeyValuePair<string, string>> ParseText(byte[] raw)
    {
        string text = DecodeText(raw);
        var pairs = new List<KeyValuePair<string, string>>();
        string section = "";

        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }
            if (line[0] == '[' && line[^1] == ']')
            {
                section = line[1..^1].Trim();
                continue;
            }
            int index = line.IndexOf('=');
            if (index < 0)
            {
                continue;
            }
            string key = line[..index].Trim();
            if (key.Length == 0)
            {
                continue;
            }
            string value = line[(index + 1)..].Trim();
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            {
                value = value[1..^1].Replace("\"\"", "\"");
            }
            pairs.Add(new KeyValuePair<string, string>(section.Length > 0 ? section + "." + key : key, value));
        }
        return pairs;
    }

    private static string EmitText(string name, List<KeyValuePair<string, string>> pairs, string source)
    {
        var builder = new StringBuilder();
        builder.Append("// <auto-generated>\n");
        builder.Append($"// 由 Godot 的 --text-export 从 {source} 生成，不要手改。\n");
        builder.Append("// 重新生成：Godot.exe --path Godot --headless -- --text-export\n");
        builder.Append("// </auto-generated>\n\n");
        builder.Append("using System;\nusing System.Collections.Generic;\n\n");
        builder.Append("namespace WorldEdit.Assets.Generated;\n\n");
        builder.Append("public static partial class War3Texts\n{\n");
        builder.Append($"    // {source}：{pairs.Count} 个键\n");
        builder.Append($"    private static Dictionary<string, string> _cached{name};\n\n");
        builder.Append($"    public static IReadOnlyDictionary<string, string> {name}() =>\n");
        builder.Append($"        _cached{name} ??= Build(\n        [\n");
        foreach (var pair in pairs)
        {
            builder.Append($"            [{CSharpLiteral(pair.Key)}, {CSharpLiteral(pair.Value)}],\n");
        }
        builder.Append("        ]);\n}\n");
        return builder.ToString();
    }

    /// <summary>
    /// --text-export[=输出目录]
    /// 把 MPQ 里全部字符串表 .txt 转成 C# 字典，一张表一个文件。
    /// </summary>
    public static void ExportTexts(string targetDirectory)
    {
        string target = CliPath.ResolveOutput(string.IsNullOrEmpty(targetDirectory)
            ? DefaultTextTarget
            : targetDirectory);
        List<string> files = War3Files(".txt");
        // Custom_V0 / Melee_V0 这些变体排在后面，让根目录先占短名
        files.Sort((a, b) =>
        {
            int priorityA = IsVariant(a) ? 1 : 0;
            int priorityB = IsVariant(b) ? 1 : 0;
            if (priorityA != priorityB)
            {
                return priorityA - priorityB;
            }
            return string.CompareOrdinal(a, b);
        });
        Console.WriteLine($"[TEXT] 源 {WorldEdit.Assets.War3Assets.GameRoot} 的 MPQ，共 {files.Count} 个 .txt");

        ClearGenerated(target);
        var used = new HashSet<string>(StringComparer.Ordinal);
        long totalKeys = 0;
        long totalBytes = 0;
        int written = 0;
        int skipped = 0;

        foreach (string relative in files)
        {
            byte[] data;
            try
            {
                data = ReadEntry(relative);
            }
            catch (Exception e)
            {
                Console.WriteLine($"  [跳过] {relative}: {e.Message}");
                skipped++;
                continue;
            }

            List<KeyValuePair<string, string>> pairs = ParseText(data);
            if (pairs.Count == 0)
            {
                skipped++;
                continue;
            }

            string baseName = TrimToName(Path.GetFileNameWithoutExtension(relative), "Text");
            string name = baseName;
            if (!used.Add(name))
            {
                string directory = relative.Split('\\')[0];
                name = TrimToName(directory + baseName, "Text");
                used.Add(name);
            }

            string text = EmitText(name, pairs, relative.Replace('\\', '/'));
            WriteText(Path.Combine(target, name + ".cs"), text);
            totalBytes += Encoding.UTF8.GetByteCount(text);
            totalKeys += pairs.Count;
            written++;
            Console.WriteLine($"  {relative,-52} {pairs.Count,5} 键  {Encoding.UTF8.GetByteCount(text) / 1024.0,6:0.0} KB -> {name}.cs");
        }

        WriteText(Path.Combine(target, "_War3Texts.cs"),
            "// <auto-generated>\n"
            + "// 由 Godot 的 --text-export 生成，提供所有文本表共用的索引构建方法。\n"
            + "// </auto-generated>\n\n"
            + "using System;\nusing System.Collections.Generic;\n\n"
            + "namespace WorldEdit.Assets.Generated;\n\n"
            + "public static partial class War3Texts\n{\n"
            + "    private static Dictionary<string, string> Build(string[][] rows)\n    {\n"
            + "        var map = new Dictionary<string, string>(rows.Length, StringComparer.OrdinalIgnoreCase);\n"
            + "        foreach (string[] row in rows)\n        {\n"
            + "            map[row[0]] = row[1];\n        }\n"
            + "        return map;\n    }\n}\n");
        WriteText(Path.Combine(target, ".gdignore"), "");

        Console.WriteLine($"\n[TEXT] {written} 个表、{totalKeys} 个键、{totalBytes / 1048576.0:0.00} MB"
            + $"（跳过 {skipped} 个没有键值的文件）");
        Console.WriteLine($"[TEXT] 目录 {target}");
    }

    private static bool IsVariant(string relative)
    {
        string first = relative.Split('\\')[0];
        return first.StartsWith("Custom_", StringComparison.Ordinal)
            || first.StartsWith("Melee_", StringComparison.Ordinal);
    }
}
