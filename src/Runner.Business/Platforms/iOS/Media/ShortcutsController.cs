using Foundation;
using UIKit;

namespace Runner.Business.Platforms.iOS.Media;

/// <summary>用 shortcuts:// URL 运行「快捷指令」App 里的同名快捷指令，动作内容由快捷指令自己决定。</summary>
public static class ShortcutsController
{
    /// <summary>请求运行快捷指令，返回是否成功打开 Shortcuts；名称须与快捷指令 App 里完全一致。主线程调用。</summary>
    public static Task<bool> RunAsync(string shortcutName) =>
        UIApplication.SharedApplication.OpenUrlAsync(
            new NSUrl($"shortcuts://run-shortcut?name={Uri.EscapeDataString(shortcutName)}"),
            new UIApplicationOpenUrlOptions());
}
