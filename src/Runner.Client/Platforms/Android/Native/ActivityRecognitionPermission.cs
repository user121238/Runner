using System.Diagnostics;

namespace Runner.Client.Platforms.Android.Native
{
    /// <summary>
    /// 活动识别权限
    /// Android 10+ 需要此权限才能使用计步传感器
    /// </summary>
    public class ActivityRecognitionPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            new[]
            {
                (global::Android.Manifest.Permission.ActivityRecognition, true)
            };
    }
}
