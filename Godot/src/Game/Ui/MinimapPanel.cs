using Godot;

namespace WorldEdit;

/// <summary>
/// 视口左上角的小地图。图片由占位高度场现算，等 W3E 落地后换成 Terrain.MinimapImage()。
/// 外观对齐 HiveWE：256×256、方角、只有 1px 等宽细边，没有内边距也没有标题。
/// </summary>
public partial class MinimapPanel : PanelContainer
{
    private const int MapTexSize = 256;

    public MinimapPanel()
    {
        CustomMinimumSize = new Vector2(MapTexSize, MapTexSize);
        SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        MouseFilter = Control.MouseFilterEnum.Stop;

        var sb = new StyleBoxFlat
        {
            BgColor = Colors.Black,
            BorderColor = Color.FromHtml("d2d2d2"),
        };
        sb.SetBorderWidthAll(1);
        sb.SetCornerRadiusAll(0);
        // 关键：子控件只按 content margin 内缩，不会自动让开 border，必须留出 1px 否则图片把边框盖住
        sb.ContentMarginLeft = 1;
        sb.ContentMarginRight = 1;
        sb.ContentMarginTop = 1;
        sb.ContentMarginBottom = 1;
        AddThemeStyleboxOverride("panel", sb);
    }

    public override void _Ready()
    {
        var tex = BuildTexture();
        var btn = new TextureButton
        {
            TextureNormal = tex,
            StretchMode = TextureButton.StretchModeEnum.Scale,
            IgnoreTextureSize = false,
            TooltipText = "小地图（点击移动相机，打桩）",
            CustomMinimumSize = new Vector2(MapTexSize, MapTexSize),
        };
        btn.Pressed += () => Stub.Fire("minimap.click");
        AddChild(btn);
    }

    private static Texture2D BuildTexture()
    {
        var img = Image.CreateEmpty(MapTexSize, MapTexSize, false, Image.Format.Rgb8);
        float world = ViewportPanel.Cells * ViewportPanel.CellSize;

        for (int y = 0; y < MapTexSize; y++)
        {
            for (int x = 0; x < MapTexSize; x++)
            {
                float wx = x / (float)MapTexSize * world;
                float wz = y / (float)MapTexSize * world;
                float h = ViewportPanel.HeightAt(wx, wz);

                Color c;
                if (h < ViewportPanel.WaterLevel)
                {
                    float depth = Mathf.Clamp((ViewportPanel.WaterLevel - h) / 8f, 0f, 1f);
                    c = new Color(0.10f + 0.06f * depth, 0.22f + 0.08f * depth, 0.34f + 0.10f * depth);
                }
                else
                {
                    float t = Mathf.Clamp((h - ViewportPanel.WaterLevel) / 22f, 0f, 1f);
                    c = new Color(0.17f + 0.13f * t, 0.26f + 0.10f * t, 0.14f + 0.05f * t);
                }
                img.SetPixel(x, y, c);
            }
        }

        // 网格参考线，让人一眼看出是地图而不是色块
        var gridColor = new Color(0, 0, 0, 0.18f);
        for (int i = 1; i < 8; i++)
        {
            int p = i * MapTexSize / 8;
            for (int k = 0; k < MapTexSize; k++)
            {
                img.SetPixel(p, k, img.GetPixel(p, k).Blend(gridColor));
                img.SetPixel(k, p, img.GetPixel(k, p).Blend(gridColor));
            }
        }

        return ImageTexture.CreateFromImage(img);
    }
}
