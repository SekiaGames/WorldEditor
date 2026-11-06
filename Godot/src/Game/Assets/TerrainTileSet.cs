using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.Slk;

namespace WorldEdit.Assets;

/// <summary>
/// War3 原始资源访问：优先读转好的 PNG/glb（`assets/converted/`），
/// 没有再回到游戏安装目录的 MPQ（`WAR3.MPQ` / `War3x.mpq` …）。
///
/// 以前是在仓库里放一份解包出来的 `war3/`（585 MB、上万文件），现在直接读安装目录，
/// 不再保留副本。MPQ 读取走 <see cref="WorldEdit.Core.Formats.Mpq.MpqArchive"/>（按名哈希查找，
/// 大小写不敏感、路径用反斜杠）。
/// </summary>
public static class War3Assets
{
    /// <summary>游戏安装目录（含 *.mpq）。默认值可用 `--game-dir=` 覆盖。</summary>
    public static string GameRoot { get; private set; } = "D:/War3_1.27a";

    /// <summary>
    /// 由安装目录批量转出来的 PNG（`--image-convert`），已经过 Godot 导入，可以直接 load。
    /// 图片优先走这里：不再解码 BLP，颜色由导入参数决定（无损，compress/mode=0）。
    /// </summary>
    public const string ConvertedRoot = "res://assets/converted/";

    /// <summary>
    /// 归档查找顺序：越靠前优先级越高。War3 是「后加载的覆盖先加载的」，
    /// 所以补丁包要排在基础包前面。
    /// </summary>
    private static readonly string[] ArchiveNames =
    {
        "zhCN.mpq", "Patch127.mpq", "patch.mpq", "War3Patch.mpq",
        "War3xlocal.mpq", "War3x.mpq", "WAR3.MPQ",
    };

    private static WorldEdit.Core.Formats.Mpq.MpqArchive[] _archives;

    public static void SetGameRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        GameRoot = path.Replace('\\', '/').TrimEnd('/');
        _archives = null;
        GD.Print($"[ASSET] 游戏目录 = {GameRoot}");
    }

    private static WorldEdit.Core.Formats.Mpq.MpqArchive[] Archives
    {
        get
        {
            if (_archives != null)
            {
                return _archives;
            }

            var opened = new List<WorldEdit.Core.Formats.Mpq.MpqArchive>();
            foreach (string name in ArchiveNames)
            {
                string path = $"{GameRoot}/{name}";
                if (!System.IO.File.Exists(path))
                {
                    continue;
                }
                try
                {
                    opened.Add(WorldEdit.Core.Formats.Mpq.MpqArchive.Open(path));
                }
                catch (Exception e)
                {
                    GD.PushWarning($"[ASSET] 打不开 {name}: {e.Message}");
                }
            }

            GD.Print($"[ASSET] 已挂载 {opened.Count} 个 MPQ：{string.Join(",", opened.ConvertAll(a => System.IO.Path.GetFileName(a.Path)))}");
            _archives = opened.ToArray();
            return _archives;
        }
    }

    public static bool Exists(string relative)
    {
        if (ConvertedPath(relative) != null)
        {
            return true;
        }

        string rel = relative.Replace('/', '\\');
        foreach (var archive in Archives)
        {
            if (archive.Contains(rel))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 同名不同扩展名的（x.blp / x.tga 并存）在转换时写成了 x.blp.png / x.tga.png，
    /// 所以两种命名都要试。返回 null 表示这张图没有被转换过。
    /// </summary>
    private static string ConvertedPath(string relative)
    {
        string rel = relative.Replace('\\', '/');
        int slash = rel.LastIndexOf('/');
        string directory = slash >= 0 ? rel[..(slash + 1)] : "";
        string file = slash >= 0 ? rel[(slash + 1)..] : rel;
        int dot = file.LastIndexOf('.');
        if (dot > 0)
        {
            file = file[..dot];
        }

        string plain = ConvertedRoot + directory + file + ".png";
        if (ResourceLoader.Exists(plain))
        {
            return plain;
        }

        string withExtension = ConvertedRoot + rel + ".png";
        return ResourceLoader.Exists(withExtension) ? withExtension : null;
    }

    private static Image LoadConverted(string relative)
    {
        string path = ConvertedPath(relative);
        if (path == null)
        {
            return null;
        }

        var texture = ResourceLoader.Load<Texture2D>(path);
        Image image = texture?.GetImage();
        image?.Convert(Image.Format.Rgba8);
        return image;
    }

    public static byte[] Read(string relative)
    {
        string rel = relative.Replace('/', '\\');
        foreach (var archive in Archives)
        {
            byte[] data = archive.TryRead(rel);
            if (data != null && data.Length > 0)
            {
                return data;
            }
        }
        throw new System.IO.FileNotFoundException($"资源不存在: {rel}（游戏目录 {GameRoot}）");
    }

    public static Image LoadImage(string relative)
    {
        Image converted = LoadConverted(relative);
        if (converted != null)
        {
            return converted;
        }
        return BlpDecoder.Decode(Read(relative));
    }
}

/// <summary>
/// 地形贴图集：把 w3e 里的 4 字符 tileID 映射到 BLP 文件，并合成 sampler2DArray。
/// 同一个 tileset 的贴图尺寸一致（512×256），可以直接叠成 texture array。
/// </summary>
public sealed class TerrainTileSet
{

    /// <summary>「疫病地表」不是 Terrain.slk 里的一行，用这个伪 id 表示。</summary>
    public const string BlightId = "@blight";

    private static string _blightPath = "TerrainArt/Blight/Ashen_Blight.blp";

    /// <summary>按 tileset 选对应的疫病贴图（洛丹伦夏 → Lords_Blight）。</summary>
    public static void SetBlightTileset(char tileset)
    {
        _blightPath = tileset switch
        {
            'A' => "TerrainArt/Blight/Ashen_Blight.blp",
            'B' => "TerrainArt/Blight/Barrens_Blight.blp",
            'D' => "TerrainArt/Blight/Cave_Blight.blp",
            'F' => "TerrainArt/Blight/Felwood_Blight.blp",
            'G' => "TerrainArt/Blight/G_Blight.blp",
            'L' => "TerrainArt/Blight/Lords_Blight.blp",
            'N' => "TerrainArt/Blight/North_Blight.blp",
            'X' => "TerrainArt/Blight/Village_Blight.blp",
            'Y' => "TerrainArt/Blight/VillageFall_Blight.blp",
            'Z' => "TerrainArt/Blight/Lordf_Blight.blp",
            _ => "TerrainArt/Blight/Ashen_Blight.blp",
        };
    }

    private static SlkTable _slk;
    private readonly Dictionary<string, Image> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);

    private static SlkTable Table => _slk ??= Generated.War3Tables.Terrain();

    /// <summary>tileID（如 Ldrt）→ 贴图相对路径。</summary>
    public static string PathFor(string tileId)
    {
        int row = Table.FindRow("tileID", tileId);
        if (row < 0)
        {
            return null;
        }
        string dir = Table.Get(row, "dir");
        string file = Table.Get(row, "file");
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(file))
        {
            return null;
        }
        return dir.Replace('\\', '/') + "/" + file + ".blp";
    }

    public static string NameFor(string tileId)
    {
        int row = Table.FindRow("tileID", tileId);
        return row < 0 ? tileId : Table.Get(row, "comment") ?? tileId;
    }

    public Image Get(string tileId)
    {
        if (_images.TryGetValue(tileId, out Image cached))
        {
            return cached;
        }

        string relative = tileId == BlightId ? _blightPath : PathFor(tileId);
        if (relative == null || !War3Assets.Exists(relative))
        {
            GD.PushWarning($"[TEX] 找不到瓦片贴图: {tileId} -> {relative}");
            return null;
        }

        Image image = War3Assets.LoadImage(relative);
        _images[tileId] = image;
        _paths[tileId] = relative;
        return image;
    }

    public string PathOf(string tileId)
    {
        Get(tileId);
        return _paths.GetValueOrDefault(tileId);
    }
}
