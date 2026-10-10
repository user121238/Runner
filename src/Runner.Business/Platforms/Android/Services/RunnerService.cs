using _Microsoft.Android.Resource.Designer;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace Runner.Business.Platforms.Android.Services
{

    [Service(ForegroundServiceType = ForegroundService.TypeLocation | ForegroundService.TypeMediaPlayback, Exported = false, Enabled = true, Name = "com.eric.runner.RunnerService")]
    public class RunnerService : Service
    {
        public override IBinder? OnBind(Intent? intent) => null;

        private const int NOTIFICATION_ID = 1001;
        private const string CHANNEL_ID = "runner_channel";
        public const string UseLocationExtra = "runner.use_location";

        /// <summary>通知小图标的资源 id，由应用在启动时指定（资源在应用工程里）；默认用系统图标兜底。</summary>
        public static int SmallIconResourceId { get; set; } = global::Android.Resource.Drawable.IcMenuCompass;

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {

            // 1. 创建通知渠道 (Android 8.0+)
            CreateNotificationChannel();


            // 2. 构建通知，点击通知可以回到应用
            var launchIntent = PackageManager?.GetLaunchIntentForPackage(PackageName!);
            var pendingIntent = launchIntent != null
                ? PendingIntent.GetActivity(this, 0, launchIntent, PendingIntentFlags.Immutable)
                : null;

            var notification = new Notification.Builder(this, CHANNEL_ID)
                .SetContentTitle("跑步进行中")
                .SetContentText("点击返回 Runner")
                .SetSmallIcon(SmallIconResourceId)
                .SetContentIntent(pendingIntent)
                .SetOngoing(true)
                .Build();


            // 3. 启动前台服务
            // 无定位授权时仍可只运行节拍/计步；不要让 manifest 的全部类型触发位置权限异常。
            var serviceType = ForegroundService.TypeMediaPlayback;
            bool locationGranted = CheckSelfPermission(global::Android.Manifest.Permission.AccessFineLocation) == global::Android.Content.PM.Permission.Granted
                || CheckSelfPermission(global::Android.Manifest.Permission.AccessCoarseLocation) == global::Android.Content.PM.Permission.Granted;
            var locations = GetSystemService(LocationService) as global::Android.Locations.LocationManager;
            if (intent?.GetBooleanExtra(UseLocationExtra, false) == true && locationGranted && locations?.IsLocationEnabled == true)
                serviceType |= ForegroundService.TypeLocation;
            StartForeground(NOTIFICATION_ID, notification, serviceType);

            // 会话在应用内存中；进程消失后不重建一个没有会话的“跑步中”通知。
            return StartCommandResult.NotSticky;

        }


        /// <summary>
        /// 创建通知渠道
        /// </summary>
        private void CreateNotificationChannel()
        {
            var channel = new NotificationChannel(CHANNEL_ID, "Runner", NotificationImportance.Low);
            var notificationManager = GetSystemService(NotificationService) as NotificationManager;
            notificationManager?.CreateNotificationChannel(channel);
        }



    }
}
