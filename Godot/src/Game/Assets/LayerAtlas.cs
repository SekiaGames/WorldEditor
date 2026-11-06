using Godot;

namespace WorldEdit.Assets;

/// <summary>
/// 把等尺寸图层竖向拼成一张图集（atlas），替代 <c>Texture2DArray</c>。
///
/// 为什么不用 Texture2DArray：它是渲染资源，Godot 没有序列化器——存成 .res 只剩尺寸元数据，
/// 像素全丢。而 Demo 场景要求「不运行也能静态观察」，材质必须能直接引用一张普通 PNG。
///
/// 采样约定：第 i 层在图集的 y 区间是 [i/n, (i+1)/n]，所以 shader 里
/// <c>vec2(uv.x, (uv.y + layer) / layerCount)</c> 就能取到对应层。
/// 代价是相邻层会在 mipmap 里轻微混色（地表瓦片本来就取自同一张图集，肉眼几乎看不出）。
/// </summary>
public static class LayerAtlas
{
    public static Texture2D Build(Godot.Collections.Array<Image> images, out int layers)
    {
        layers = images.Count;
        if (layers == 0)
        {
            return null;
        }

        Image first = images[0];
        int width = first.GetWidth();
        int height = first.GetHeight();

        Image atlas = Image.CreateEmpty(width, height * layers, false, Image.Format.Rgba8);
        for (int i = 0; i < layers; i++)
        {
            Image layer = images[i];
            if (layer.GetWidth() != width || layer.GetHeight() != height)
            {
                layer = Resize(layer, width, height);
            }
            atlas.BlitRect(layer, new Rect2I(0, 0, width, height), new Vector2I(0, i * height));
        }

        atlas.GenerateMipmaps();
        return ImageTexture.CreateFromImage(atlas);
    }

    private static Image Resize(Image image, int width, int height)
    {
        var copy = (Image)image.Duplicate();
        copy.Resize(width, height);
        return copy;
    }
}
