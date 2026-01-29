using _Microsoft.Android.Resource.Designer;
using CommunityToolkit.Mvvm.Input;
using DM.Maui.Framework.Abstractions.Navigation;
using DM.Maui.Framework.Core.Mvvm;
using Microsoft.Extensions.Logging;
using Runner.Business.Platforms.Android.StepCounter;
using Runner.Business.Platforms.Android.Media;
using Runner.Business.Platforms.Android.Permissions;
using Runner.Business.Platforms.Android.Location;
using Runner.Business.Location;

namespace Runner.Client.ViewModels;

/// <summary>
/// 主页 ViewModel - 展示框架核心功能
/// </summary>
public partial class MainViewModel : BaseViewModel
{
    private readonly CadencePlay _cadencePlay;
    private IStepCounter? _stepCounter;
    private bool _isPedometerActive = false;
    private ILocationTracker? _locationTracker;

    // 计步器相关属性
    private int _stepCount = 0;
    private double _stepFrequency = 0.0;
    private string _pedometerStatus = "未启动";

    // GPS 相关属性
    private double _gpsDistance = 0.0;
    private double _gpsSpeed = 0.0;
    private string _gpsStatus = "未启动";

    public MainViewModel(ILogger<MainViewModel> logger, INavigationService navigation) : base(logger, navigation)
    {
        _cadencePlay = new CadencePlay();
        // 延迟初始化音频资源，避免阻塞UI线程

        Task.Run(() => _cadencePlay.Init(ResourceConstant.Raw.beep3));

        // 初始化计步器
        InitPedometer();

        // 初始化GPS追踪器
        InitGpsTracker();
    }

    private void InitPedometer()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] 开始初始化计步器");

            if (DeviceInfo.Current.Platform != DevicePlatform.Android)
            {
                _pedometerStatus = "仅支持Android设备";
                System.Diagnostics.Debug.WriteLine("[MainViewModel] 非Android平台，跳过初始化");
                return;
            }

            // 在主线程上异步请求权限
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await RequestActivityRecognitionPermission();
            });
        }
        catch (Exception ex)
        {
            _pedometerStatus = $"初始化失败: {ex.Message}";
            Logger?.LogError(ex, "初始化计步器失败");
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 初始化失败: {ex}");
        }
    }

    private async Task RequestActivityRecognitionPermission()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] 检查活动识别权限");

            var status = await Permissions.CheckStatusAsync<ActivityRecognitionPermission>();
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 当前权限状态: {status}");

            if (status != PermissionStatus.Granted)
            {
                System.Diagnostics.Debug.WriteLine("[MainViewModel] 请求活动识别权限");
                status = await Permissions.RequestAsync<ActivityRecognitionPermission>();
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 权限请求结果: {status}");
            }

            if (status == PermissionStatus.Granted)
            {
                System.Diagnostics.Debug.WriteLine("[MainViewModel] 权限已授予，创建计步器");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CreateStepCounter();
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 权限被拒绝: {status}");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _pedometerStatus = "需要活动识别权限";
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 权限请求异常: {ex}");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _pedometerStatus = $"权限请求失败: {ex.Message}";
            });
        }
    }

    private void CreateStepCounter()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] 调用工厂创建计步器");
            var context = Platform.CurrentActivity!;
            _stepCounter = StepCounterFactory.Create(context);

            // 订阅事件
            _stepCounter.OnStepCountChanged += OnStepCountChanged;
            _stepCounter.OnStepFrequencyChanged += OnStepFrequencyChanged;

            // 显示传感器类型
            string sensorType = _stepCounter is HardwareStepCounter ? "硬件传感器" : "加速度传感器";
            _pedometerStatus = $"使用{sensorType}";

            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 计步器初始化成功 - {sensorType}");
        }
        catch (Exception ex)
        {
            _pedometerStatus = $"初始化失败: {ex.Message}";
            Logger?.LogError(ex, "初始化计步器失败");
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 创建计步器失败: {ex}");
        }
    }

    private void OnStepCountChanged(int steps)
    {
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] 收到步数变化: {steps}");

        // 在主线程上更新UI
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StepCount = steps;
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] UI已更新步数: {steps}");
        });
    }

    private void OnStepFrequencyChanged(double spm)
    {
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] 收到步频变化: {spm:F1} SPM");

        // 在主线程上更新UI
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StepFrequency = spm;
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] UI已更新步频: {spm:F1}");
        });
    }

    [RelayCommand]
    private void NextTrack()
    {
        var context = Platform.CurrentActivity!;
        MediaController.NextTrack(context);
    }

    [RelayCommand]
    private void PreviousTrack()
    {
        var context = Platform.CurrentActivity!;
        MediaController.PreviousTrack(context);
    }

    [RelayCommand]
    private void PlayPause()
    {
        var context = Platform.CurrentActivity!;
        MediaController.PlayPause(context);
    }

    [RelayCommand]
    private void StartTick()
    {
        _cadencePlay.Start(180);
    }

    [RelayCommand]
    private void StopTick()
    {
        _cadencePlay.Stop();
    }

    #region 计步器相关属性

    public int StepCount
    {
        get => _stepCount;
        private set => SetProperty(ref _stepCount, value);
    }

    public double StepFrequency
    {
        get => _stepFrequency;
        private set => SetProperty(ref _stepFrequency, value);
    }

    public string PedometerStatus
    {
        get => _pedometerStatus;
        private set => SetProperty(ref _pedometerStatus, value);
    }

    #endregion

    #region 计步器相关命令

    [RelayCommand]
    private void StartPedometer()
    {
        System.Diagnostics.Debug.WriteLine("[MainViewModel] 开始计步命令");

        if (_stepCounter == null || _isPedometerActive)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 无法启动 - 计步器为空: {_stepCounter == null}, 已激活: {_isPedometerActive}");
            return;
        }

        try
        {
            _stepCounter.Start();
            _isPedometerActive = true;
            PedometerStatus = "运行中";
            System.Diagnostics.Debug.WriteLine("[MainViewModel] 计步器已启动");
        }
        catch (Exception ex)
        {
            PedometerStatus = $"启动失败: {ex.Message}";
            Logger?.LogError(ex, "启动计步器失败");
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 启动失败: {ex}");
        }
    }

    [RelayCommand]
    private void StopPedometer()
    {
        System.Diagnostics.Debug.WriteLine("[MainViewModel] 停止计步命令");

        if (_stepCounter == null || !_isPedometerActive)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 无法停止 - 计步器为空: {_stepCounter == null}, 未激活: {!_isPedometerActive}");
            return;
        }

        try
        {
            _stepCounter.Stop();
            _isPedometerActive = false;

            string sensorType = _stepCounter is HardwareStepCounter ? "硬件传感器" : "加速度传感器";
            PedometerStatus = $"使用{sensorType}";
            System.Diagnostics.Debug.WriteLine("[MainViewModel] 计步器已停止");
        }
        catch (Exception ex)
        {
            PedometerStatus = $"停止失败: {ex.Message}";
            Logger?.LogError(ex, "停止计步器失败");
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 停止失败: {ex}");
        }
    }

    [RelayCommand]
    private void ResetStepCount()
    {
        StepCount = 0;
        StepFrequency = 0.0;
    }

    [RelayCommand]
    private void SwitchToAccelerometer()
    {
        System.Diagnostics.Debug.WriteLine("[MainViewModel] 切换到加速度传感器");

        try
        {
            // 停止当前计步器
            if (_stepCounter != null)
            {
                if (_isPedometerActive)
                {
                    _stepCounter.Stop();
                    _isPedometerActive = false;
                }

                _stepCounter.OnStepCountChanged -= OnStepCountChanged;
                _stepCounter.OnStepFrequencyChanged -= OnStepFrequencyChanged;
                _stepCounter.Dispose();
                _stepCounter = null;
            }

            // 强制使用加速度传感器
            StepCounterFactory.ForceAccelerometer = true;

            // 重新创建计步器
            CreateStepCounter();

            System.Diagnostics.Debug.WriteLine("[MainViewModel] 已切换到加速度传感器");
        }
        catch (Exception ex)
        {
            _pedometerStatus = $"切换失败: {ex.Message}";
            Logger?.LogError(ex, "切换到加速度传感器失败");
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 切换失败: {ex}");
        }
    }

    [RelayCommand]
    private void SwitchToHardware()
    {
        System.Diagnostics.Debug.WriteLine("[MainViewModel] 切换到硬件传感器");

        try
        {
            // 停止当前计步器
            if (_stepCounter != null)
            {
                if (_isPedometerActive)
                {
                    _stepCounter.Stop();
                    _isPedometerActive = false;
                }

                _stepCounter.OnStepCountChanged -= OnStepCountChanged;
                _stepCounter.OnStepFrequencyChanged -= OnStepFrequencyChanged;
                _stepCounter.Dispose();
                _stepCounter = null;
            }

            // 使用硬件传感器
            StepCounterFactory.ForceAccelerometer = false;

            // 重新创建计步器
            CreateStepCounter();

            System.Diagnostics.Debug.WriteLine("[MainViewModel] 已切换到硬件传感器");
        }
        catch (Exception ex)
        {
            _pedometerStatus = $"切换失败: {ex.Message}";
            Logger?.LogError(ex, "切换到硬件传感器失败");
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 切换失败: {ex}");
        }
    }

    #endregion

    #region GPS 距离追踪

    private void InitGpsTracker()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] 开始初始化GPS追踪器");

            if (DeviceInfo.Current.Platform != DevicePlatform.Android)
            {
                _gpsStatus = "仅支持Android设备";
                System.Diagnostics.Debug.WriteLine("[MainViewModel] 非Android平台，跳过初始化");
                return;
            }

            // 在主线程上异步请求权限
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await RequestLocationPermission();
            });
        }
        catch (Exception ex)
        {
            _gpsStatus = $"初始化失败: {ex.Message}";
            Logger?.LogError(ex, "初始化GPS追踪器失败");
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] GPS初始化失败: {ex}");
        }
    }

    private async Task RequestLocationPermission()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] 检查位置权限");

            var status = await Permissions.CheckStatusAsync<LocationPermission>();
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 当前位置权限状态: {status}");

            if (status != PermissionStatus.Granted)
            {
                System.Diagnostics.Debug.WriteLine("[MainViewModel] 请求位置权限");
                status = await Permissions.RequestAsync<LocationPermission>();
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 位置权限请求结果: {status}");
            }

            if (status == PermissionStatus.Granted)
            {
                System.Diagnostics.Debug.WriteLine("[MainViewModel] 位置权限已授予，创建GPS追踪器");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CreateLocationTracker();
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 位置权限被拒绝: {status}");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _gpsStatus = "需要位置权限";
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 位置权限请求异常: {ex}");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _gpsStatus = $"权限请求失败: {ex.Message}";
            });
        }
    }

    private void CreateLocationTracker()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] 创建GPS追踪器");
            var context = Platform.CurrentActivity!;

            // 使用更宽松的测试配置
            var filterConfig = new Runner.Core.Geolocation.LocationFilterConfig
            {
                MinDistanceMeters = 2.0,        // 降低到2米（更敏感）
                MaxSpeedKmPerHour = 20.0,       // 提高到20公里/小时
                MinTimeIntervalSeconds = 0.3    // 降低到0.3秒
            };

            _locationTracker = new AndroidLocationTracker(context, filterConfig);

            // 订阅事件
            _locationTracker.OnDistanceUpdated += OnGpsDistanceUpdated;
            _locationTracker.OnSpeedUpdated += OnGpsSpeedUpdated;
            _locationTracker.OnError += OnGpsError;

            _gpsStatus = "已就绪（测试模式）";
            System.Diagnostics.Debug.WriteLine("[MainViewModel] GPS追踪器创建成功（测试配置）");
        }
        catch (Exception ex)
        {
            _gpsStatus = $"创建失败: {ex.Message}";
            Logger?.LogError(ex, "创建GPS追踪器失败");
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 创建GPS追踪器失败: {ex}");
        }
    }

    private void OnGpsDistanceUpdated(double distanceMeters)
    {
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] GPS距离更新: {distanceMeters:F1}米");

        MainThread.BeginInvokeOnMainThread(() =>
        {
            GpsDistance = distanceMeters / 1000.0; // 转换为公里
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] UI已更新GPS距离: {GpsDistance:F2}公里");
        });
    }

    private void OnGpsSpeedUpdated(double speedKmh)
    {
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] GPS速度更新: {speedKmh:F1}公里/小时");

        MainThread.BeginInvokeOnMainThread(() =>
        {
            GpsSpeed = speedKmh;
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] UI已更新GPS速度: {GpsSpeed:F1}公里/小时");
        });
    }

    private void OnGpsError(string error)
    {
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] GPS错误: {error}");

        MainThread.BeginInvokeOnMainThread(() =>
        {
            GpsStatus = $"错误: {error}";
        });
    }

    public double GpsDistance
    {
        get => _gpsDistance;
        private set => SetProperty(ref _gpsDistance, value);
    }

    public double GpsSpeed
    {
        get => _gpsSpeed;
        private set => SetProperty(ref _gpsSpeed, value);
    }

    public string GpsStatus
    {
        get => _gpsStatus;
        private set => SetProperty(ref _gpsStatus, value);
    }

    [RelayCommand]
    private async Task StartGpsTracking()
    {
        System.Diagnostics.Debug.WriteLine("[MainViewModel] 开始GPS追踪命令");

        if (_locationTracker == null)
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] GPS追踪器未初始化");
            GpsStatus = "追踪器未初始化";
            return;
        }

        if (_locationTracker.IsTracking)
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] GPS已在追踪中");
            return;
        }

        try
        {
            await _locationTracker.StartAsync();
            GpsStatus = "追踪中";
            System.Diagnostics.Debug.WriteLine("[MainViewModel] GPS追踪已启动");
        }
        catch (Exception ex)
        {
            GpsStatus = $"启动失败: {ex.Message}";
            Logger?.LogError(ex, "启动GPS追踪失败");
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] GPS启动失败: {ex}");
        }
    }

    [RelayCommand]
    private void StopGpsTracking()
    {
        System.Diagnostics.Debug.WriteLine("[MainViewModel] 停止GPS追踪命令");

        if (_locationTracker == null || !_locationTracker.IsTracking)
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] GPS未在追踪中");
            return;
        }

        try
        {
            _locationTracker.Stop();
            GpsStatus = "已停止";
            System.Diagnostics.Debug.WriteLine("[MainViewModel] GPS追踪已停止");
        }
        catch (Exception ex)
        {
            GpsStatus = $"停止失败: {ex.Message}";
            Logger?.LogError(ex, "停止GPS追踪失败");
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] GPS停止失败: {ex}");
        }
    }

    [RelayCommand]
    private void ResetGpsDistance()
    {
        System.Diagnostics.Debug.WriteLine("[MainViewModel] 重置GPS距离命令");

        if (_locationTracker == null)
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] GPS追踪器未初始化");
            return;
        }

        try
        {
            _locationTracker.Reset();
            GpsDistance = 0;
            GpsSpeed = 0;
            System.Diagnostics.Debug.WriteLine("[MainViewModel] GPS距离已重置");
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "重置GPS距离失败");
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] GPS重置失败: {ex}");
        }
    }

    #endregion

    protected override void Dispose(bool disposing)
    {

        _cadencePlay?.Dispose();

        if (_stepCounter != null)
        {
            _stepCounter.OnStepCountChanged -= OnStepCountChanged;
            _stepCounter.OnStepFrequencyChanged -= OnStepFrequencyChanged;
            _stepCounter.Dispose();
            _stepCounter = null;
        }

        base.Dispose(disposing);

    }

}
