namespace Runner.Business.Platforms.Android.Permissions
{
    /// <summary>
    /// 位置权限
    /// </summary>
    public class LocationPermission : Microsoft.Maui.ApplicationModel.Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            new[]
            {
                (global::Android.Manifest.Permission.AccessFineLocation, true),
                (global::Android.Manifest.Permission.AccessCoarseLocation, true)
            };
    }
}
