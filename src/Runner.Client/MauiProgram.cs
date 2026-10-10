using _Microsoft.Android.Resource.Designer;
using DM.Maui.Framework.Data.Extensions;
using Microsoft.Extensions.Logging;
using Prism.Ioc;
using Runner.Business.Platforms.Android.Services;
using Runner.Business.Platforms.Android.Session;
using Runner.Business.Services.Activities;
using Runner.Business.Services.Ble;
using Runner.Business.Services.Database;
using Runner.Business.Services.Workouts;
using Runner.Client.ViewModels;
using Runner.Client.Views;

namespace Runner.Client
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                })
                .UsePrism(prism => prism
                    .RegisterTypes(registry =>
                    {
                        // 页面：根页承载区域；需要独立一屏的用 ContentPage 跳转
                        registry.RegisterForNavigation<MainPage, MainPageViewModel>();
                        registry.RegisterForNavigation<DiagnosticsPage, DiagnosticsPageViewModel>();
                        registry.RegisterForNavigation<WorkoutEditorPage, WorkoutEditorPageViewModel>();
                        registry.RegisterForNavigation<ActivityDetailPage, ActivityDetailPageViewModel>();

                        // 区域视图：首页 / 课程 / 记录 / 设置在 MainRegion 内切换
                        registry.RegisterForRegionNavigation<RunView, RunViewModel>();
                        registry.RegisterForRegionNavigation<WorkoutsView, WorkoutsViewModel>();
                        registry.RegisterForRegionNavigation<HistoryView, HistoryViewModel>();
                        registry.RegisterForRegionNavigation<SettingsView, SettingsViewModel>();
                    })
                    .CreateWindow("NavigationPage/MainPage"));

            // 数据库：课程与记录共用一个 LiteDB 文件；IRepository<T> 由框架按开放泛型注册
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "runner.db");
            builder.Services.AddLiteDbDataAccess(dbPath);
            builder.Services.AddSingleton<WorkoutStore>();
            builder.Services.AddSingleton<ActivityStore>();

            // 前台服务通知用同款 R 小图标：资源在应用工程里，服务在 Business，启动时指定
            RunnerService.SmallIconResourceId = ResourceConstant.Drawable.ic_stat_runner;

            // 运动会话宿主：应用级单例，持有 GPS / 计步 / 节拍器、当前课程，结束后落盘记录
            builder.Services.AddSingleton(sp => new RunSessionHost(
                ResourceConstant.Raw.beep3,
                sp.GetRequiredService<WorkoutStore>(),
                sp.GetRequiredService<ActivityStore>(),
                sp.GetRequiredService<ILogger<RunSessionHost>>()));

            // 手表线（冻结）：注册保留，界面入口已移除
            builder.Services.AddSingleton<IWatchBleService, WatchBleService>();
            builder.Services.AddSingleton<MiHealthDataReader>();
            builder.Services.AddTransient<WatchWorkoutViewModel>();
            builder.Services.AddTransient<WatchWorkoutPage>();

            // Android 的 Entry 自带 Material 下划线，和圆角输入框打架；统一去掉，输入框外观由 XAML 的 Border 负责
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) =>
            {
#if ANDROID
                handler.PlatformView.Background = null;
#endif
            });

#if DEBUG
            builder.Logging.AddDebug();
            builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif

            return builder.Build();
        }
    }
}
