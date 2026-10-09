using DM.Maui.Framework.Core.Extensions;
using DM.Maui.Framework.Data.Extensions;
using Microsoft.Extensions.Logging;
using System.Globalization;
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
                });

            // 注册框架核心服务
            builder.Services.AddMauiCore(sp=>{});

            // 注册数据库服务
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "runner.db");
            builder.Services.AddLiteDbDataAccess(dbPath);

            // 注册 BLE 服务
            builder.Services.AddSingleton<IWatchBleService, WatchBleService>();

            // 注册 MiHealth 数据库读取器
            builder.Services.AddSingleton<MiHealthDataReader>();

            // 注册 ViewModel 和页面
            builder.Services.AddTransient<WatchWorkoutViewModel>();
            builder.Services.AddTransient<WatchWorkoutPage>();

            // 注册路由
            builder.Services.AddNavigationRoutes(typeof(MainPage).Assembly);


#if DEBUG
    		builder.Logging.AddDebug();
            builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif

            return builder.Build();
        }
    }
}
