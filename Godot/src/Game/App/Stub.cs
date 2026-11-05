using Godot;

namespace WorldEdit;

/// <summary>
/// 打桩路由：界面阶段所有功能都从这里走一遍，打印 + 冒泡到 Main 显示提示。
/// 真实实现接上来时，只需要把对应 id 的分支换成实际调用。
/// </summary>
public static class Stub
{
    public static event System.Action<string> Fired;

    public static void Fire(string id)
    {
        GD.Print($"[STUB] {id}");
        Fired?.Invoke(id);
    }
}
