using LiteDB;
using Microsoft.Extensions.Logging;
using Prism.Ioc;
using Runner.Business.Session;
using Runner.Business.Services.Activities;
using Runner.Business.Services.Workouts;
using Runner.Client.ViewModels;
using Runner.Client.Views;
#if ANDROID
using _Microsoft.Android.Resource.Designer;
using Runner.Business.Platforms.Android.Services;
using Runner.Business.Platforms.Android.Session;
#elif IOS
using Runner.Business.Platforms.iOS.Session;
#endif

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

            // 保留原数据库与集合格式，仅去除本机私有 NuGet 仓储包装。
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "runner.db");
            builder.Services.AddSingleton<ILiteDatabase>(_ => new LiteDatabase(new ConnectionString
            {
                Filename = dbPath,
                Connection = ConnectionType.Shared
            }));
            builder.Services.AddSingleton<WorkoutStore>();
            builder.Services.AddSingleton<ActivityStore>();

#if ANDROID
            // 前台服务通知用同款 R 小图标：资源在应用工程里，服务在 Business，启动时指定
            RunnerService.SmallIconResourceId = ResourceConstant.Drawable.ic_stat_runner;
            builder.Services.AddSingleton<IRunPlatform>(_ => new AndroidRunPlatform(ResourceConstant.Raw.beep3));
#elif IOS
            builder.Services.AddSingleton<IRunPlatform, IosRunPlatform>();
#endif

            // 两个平台共用会话状态、课程推进与落盘逻辑，只有设备来源不同。
            builder.Services.AddSingleton<RunSessionHost>();

#if ANDROID
            // Android 的 Entry 自带 Material 下划线，和圆角输入框打架；统一去掉，输入框外观由 XAML 的 Border 负责
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) =>
            {
                handler.PlatformView.Background = null;
            });
#endif

#if DEBUG
            builder.Logging.AddDebug();
            builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif

            return builder.Build();
        }
    }
}
