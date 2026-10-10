using System.Runtime.InteropServices;
using ObjCRuntime;

namespace Runner.Business.Platforms.iOS.Media;

/// <summary>dlopen 调用 MediaRemote 私有框架，向系统「正在播放」会话发送媒体命令。私有 API：只能自签侧载，不能进 App Store。</summary>
public static class MediaRemoteController
{
    private const string FrameworkPath = "/System/Library/PrivateFrameworks/MediaRemote.framework/MediaRemote";
    private const int RtldNow = 2;
    private static readonly object _sync = new();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SendCommandDelegate(int command, IntPtr userInfo);

    private static SendCommandDelegate? _sendCommand;
    private static string? _loadError;

    /// <summary>加载失败原因；成功后为 null。失败只记一次，不反复 dlopen。</summary>
    public static string? LoadError => _loadError;

    /// <summary>首次调用时加载框架并解析 MRMediaRemoteSendCommand；任意线程可调，结果缓存。</summary>
    public static bool TryLoad()
    {
        lock (_sync)
        {
            if (_sendCommand != null) return true;
            if (_loadError != null) return false;
            try
            {
                var handle = Dlfcn.dlopen(FrameworkPath, RtldNow);
                if (handle == IntPtr.Zero)
                {
                    _loadError = $"dlopen 失败: {Dlfcn.dlerror()}";
                    return false;
                }
                var symbol = Dlfcn.dlsym(handle, "MRMediaRemoteSendCommand");
                if (symbol == IntPtr.Zero)
                {
                    _loadError = "MediaRemote 里没有 MRMediaRemoteSendCommand";
                    return false;
                }
                _sendCommand = Marshal.GetDelegateForFunctionPointer<SendCommandDelegate>(symbol);
                return true;
            }
            catch (Exception ex)
            {
                _loadError = ex.Message;
                return false;
            }
        }
    }

    /// <summary>发送媒体命令；函数本身无返回值，是否生效看系统「正在播放」是否响应。会话线程与主线程都可调。</summary>
    public static void Send(MediaRemoteCommand command)
    {
        if (!TryLoad()) throw new InvalidOperationException(_loadError ?? "MediaRemote 加载失败");
        _sendCommand!((int)command, IntPtr.Zero);
    }
}
