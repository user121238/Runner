using System.Runtime.InteropServices;

namespace Runner.Business.Platforms.iOS.Session;

/// <summary>mach_continuous_time 包含锁屏休眠，与可被用户调整的墙钟无关。</summary>
internal static partial class IosMonotonicClock
{
    private const string SystemLibrary = "/usr/lib/libSystem.dylib";
    private static readonly TimebaseInfo Timebase = ReadTimebase();

    public static long GetNanoseconds() =>
        checked((long)((UInt128)ContinuousTime() * Timebase.Numerator / Timebase.Denominator));

    private static TimebaseInfo ReadTimebase()
    {
        if (GetTimebase(out var info) != 0 || info.Denominator == 0)
            throw new InvalidOperationException("读取 iOS 单调时钟失败");
        return info;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TimebaseInfo
    {
        public uint Numerator;
        public uint Denominator;
    }

    [LibraryImport(SystemLibrary, EntryPoint = "mach_continuous_time")]
    private static partial ulong ContinuousTime();

    [LibraryImport(SystemLibrary, EntryPoint = "mach_timebase_info")]
    private static partial int GetTimebase(out TimebaseInfo info);
}
