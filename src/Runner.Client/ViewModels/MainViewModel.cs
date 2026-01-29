using CommunityToolkit.Mvvm.Input;
using DM.Maui.Framework.Abstractions.Navigation;
using DM.Maui.Framework.Core.Mvvm;
using Microsoft.Extensions.Logging;
using Runner.Client.Platforms.Android.Native;

namespace Runner.Client.ViewModels;

/// <summary>
/// 主页 ViewModel - 展示框架核心功能
/// </summary>
public partial class MainViewModel : BaseViewModel, IDisposable
{
    private readonly CadencePlay _cadencePlay;
    private IStepCounter? _stepCounter;
    private bool _isPedometerActive = false;

    // 计步器相关属性
    private int _stepCount = 0;
    private double _stepFrequency = 0.0;
    private string _pedometerStatus = "未启动";

    public MainViewModel(ILogger<MainViewModel> logger, INavigationService navigation) : base(logger, navigation)
    {
        _cadencePlay = new CadencePlay();
        // 延迟初始化音频资源，避免阻塞UI线程
        Task.Run(() => _cadencePlay.Init());
        
        // 初始化计步器
        InitPedometer();
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
            _stepCounter = StepCounterFactory.Create();

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
        MediaController.NextTrack();
    }

    [RelayCommand]
    private void PreviousTrack()
    {
        MediaController.PreviousTrack();
    }

    [RelayCommand]
    private void PlayPause()
    {
        MediaController.PlayPause();
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
    
    #endregion
    
    public void Dispose()
    {
        _cadencePlay?.Dispose();

        if (_stepCounter != null)
        {
            _stepCounter.OnStepCountChanged -= OnStepCountChanged;
            _stepCounter.OnStepFrequencyChanged -= OnStepFrequencyChanged;
            _stepCounter.Dispose();
            _stepCounter = null;
        }
    }
}
