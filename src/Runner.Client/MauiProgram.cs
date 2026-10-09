using _Microsoft.Android.Resource.Designer;
using DM.Maui.Framework.Data.Extensions;
using Microsoft.Extensions.Logging;
using Prism.Ioc;
using Runner.Business.Platforms.Android.Session;
using Runner.Business.Services.Ble;
using Runner.Business.Services.Database;
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

                        // 区域视图：首页 / 记录 / 设置在 MainRegion 内切换
                        registry.RegisterForRegionNavigation<RunView, RunViewModel>();
                        registry.RegisterForRegionNavigation<HistoryView, HistoryViewModel>();
                        registry.RegisterForRegionNavigation<SettingsView, SettingsViewModel>();
                    })
                    .CreateWindow("NavigationPage/MainPage"));

            // 数据库（P4 记录与设置使用）
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "runner.db");
            builder.Services.AddLiteDbDataAccess(dbPath);

            // 运动会话宿主：应用级单例，持有 GPS / 计步 / 节拍器
            builder.Services.AddSingleton(sp =>
                new RunSessionHost(ResourceConstant.Raw.beep3, sp.GetRequiredService<ILogger<RunSessionHost>>()));

            // 手表线（冻结）：注册保留，界面入口已移除
            builder.Services.AddSingleton<IWatchBleService, WatchBleService>();
            builder.Services.AddSingleton<MiHealthDataReader>();
            builder.Services.AddTransient<WatchWorkoutViewModel>();
            builder.Services.AddTransient<WatchWorkoutPage>();

#if DEBUG
            builder.Logging.AddDebug();
            builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif

            return builder.Build();
        }
    }
}
