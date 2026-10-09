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
                .SetContentTitle("Runner 服务正在运行")
                .SetContentText("点击返回应用")
                .SetSmallIcon(global::Android.Resource.Drawable.IcMenuCompass) // 使用系统内置图标
                .SetContentIntent(pendingIntent)
                .SetOngoing(true)
                .Build();


            // 3. 启动前台服务
            StartForeground(NOTIFICATION_ID, notification);

            return StartCommandResult.Sticky;

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

        public async Task StartBeeping(int spm)
        {
            double interval = 60000.0 / spm;

            using (var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(interval)))
            {
                while (await timer.WaitForNextTickAsync())
                {

                }
            }
        }


    }
}
