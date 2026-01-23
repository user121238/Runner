using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace Runner.Client
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override async void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            //请求通知权限（适用于 Android 13 及以上版本）
            var result = await Permissions.RequestAsync<Permissions.PostNotifications>();

            if (result == PermissionStatus.Granted)
            {
                var intent = new Intent(Platform.CurrentActivity, typeof(Runner.Client.Platforms.Android.Services.RunnerService));
                StartForegroundService(intent);
            }

            


        }
    }
}
