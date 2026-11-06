using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace WorldEdit.Core.Formats.W3x;

public sealed class W3iInfo
{
    public int FormatVersion { get; private set; }
    public int SaveCount { get; private set; }
    public int EditorVersion { get; private set; }
    public string NameRef { get; private set; }
    public string AuthorRef { get; private set; }
    public string DescriptionRef { get; private set; }
    public string PlayersRef { get; private set; }

    public static W3iInfo Parse(byte[] data)
    {
        if (data.Length < 16)
        {
            throw new InvalidDataException("war3map.w3i 太短");
        }
        int pos = 0;
        var info = new W3iInfo
        {
            FormatVersion = BitConverter.ToInt32(data, pos),
            SaveCount = BitConverter.ToInt32(data, pos + 4),
            EditorVersion = BitConverter.ToInt32(data, pos + 8),
        };
        pos = 12;
        info.NameRef = ReadString(data, ref pos);
        info.AuthorRef = ReadString(data, ref pos);
        info.DescriptionRef = ReadString(data, ref pos);
        info.PlayersRef = ReadString(data, ref pos);
        return info;
    }

    private static string ReadString(byte[] data, ref int pos)
    {
        int start = pos;
        while (pos < data.Length && data[pos] != 0)
        {
            pos++;
        }
        string value = Encoding.UTF8.GetString(data, start, pos - start);
        pos++;
        return value;
    }

    public static int ExtractStringId(string reference)
    {
        const string prefix = "TRIGSTR_";
        if (reference != null && reference.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(reference[prefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
        {
            return id;
        }
        return -1;
    }

    public string ResolveName(WtsDocument strings)
    {
        int id = ExtractStringId(NameRef);
        return id >= 0 ? strings?.Get(id) ?? NameRef : NameRef;
    }

    public static int NameStringId(byte[] w3i)
    {
        return ExtractStringId(Parse(w3i).NameRef);
    }
}
