using Android.Content.PM;

namespace Runner.Client.Platforms.Android;

public static class BlePermissionHelper
{
    private const int RequestBle = 1001;
    private static TaskCompletionSource<bool>? _pending;

    public static async Task<bool> RequestAsync()
    {
        var activity = Platform.CurrentActivity;
        if (activity == null) return false;

        var needed = new List<string>();

        if (activity.CheckSelfPermission(global::Android.Manifest.Permission.AccessFineLocation) != Permission.Granted)
            needed.Add(global::Android.Manifest.Permission.AccessFineLocation);

        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            if (activity.CheckSelfPermission(global::Android.Manifest.Permission.BluetoothScan) != Permission.Granted)
                needed.Add(global::Android.Manifest.Permission.BluetoothScan);
            if (activity.CheckSelfPermission(global::Android.Manifest.Permission.BluetoothConnect) != Permission.Granted)
                needed.Add(global::Android.Manifest.Permission.BluetoothConnect);
        }

        if (needed.Count == 0) return true;

        _pending = new TaskCompletionSource<bool>();
        activity.RequestPermissions(needed.ToArray(), RequestBle);
        return await _pending.Task.ConfigureAwait(false);
    }

    public static void OnResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        if (requestCode == RequestBle && _pending != null)
        {
            _pending.TrySetResult(grantResults.Length > 0 && grantResults.All(r => r == Permission.Granted));
            _pending = null;
        }
    }
}
