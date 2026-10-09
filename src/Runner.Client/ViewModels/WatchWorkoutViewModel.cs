using CommunityToolkit.Mvvm.Input;
using DM.Maui.Framework.Abstractions.Navigation;
using DM.Maui.Framework.Core.Mvvm;
using Microsoft.Extensions.Logging;
using Runner.Business.Models.WatchBle;
using Runner.Business.Services.Ble;
using Runner.Business.Services.Database;
using Runner.Client.Platforms.Android;

namespace Runner.Client.ViewModels;

public partial class WatchWorkoutViewModel : BaseViewModel
{
    private readonly IWatchBleService _bleService;
    private readonly MiHealthDataReader _dbReader;

    private string _connectionStatus = "未连接";
    private string _deviceName = "";
    private string _scanResult = "";
    private bool _isScanning;
    private bool _isConnecting;

    private string _elapsed = "00:00";
    private string _distance = "0.00";
    private string _heartRate = "--";
    private string _cadence = "--";
    private string _pace = "--:--";
    private string _calories = "0";
    private string _steps = "0";

    private bool _hasSummary;
    private string _totalDuration = "";
    private string _totalDistance = "";
    private string _totalCalories = "";
    private string _totalSteps = "";
    private string _avgHeartRate = "";
    private string _avgCadence = "";

    private readonly List<WatchSportInfo> _receivedData = new();

    public WatchWorkoutViewModel(ILogger<WatchWorkoutViewModel> logger, INavigationService navigation)
        : base(logger, navigation)
    {
        _bleService = App.Current?.Handler?.MauiContext?.Services.GetService<IWatchBleService>()
            ?? throw new InvalidOperationException("IWatchBleService not registered");
        _dbReader = App.Current?.Handler?.MauiContext?.Services.GetService<MiHealthDataReader>()
            ?? throw new InvalidOperationException("MiHealthDataReader not registered");

        _bleService.OnLog += msg => Logger.LogDebug("[BLE] {Msg}", msg);
        _bleService.OnConnectionChanged += OnConnectionChanged;
        _bleService.OnSportInfoReceived += OnSportInfoReceived;
        _bleService.OnDeviceFound += OnDeviceFound;
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set => SetProperty(ref _connectionStatus, value);
    }

    public string DeviceName
    {
        get => _deviceName;
        private set => SetProperty(ref _deviceName, value);
    }

    public string ScanResult
    {
        get => _scanResult;
        private set => SetProperty(ref _scanResult, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            SetProperty(ref _isScanning, value);
            OnPropertyChanged(nameof(IsNotScanning));
        }
    }

    public bool IsNotScanning => !IsScanning;

    public bool IsConnecting
    {
        get => _isConnecting;
        private set => SetProperty(ref _isConnecting, value);
    }

    public string Elapsed
    {
        get => _elapsed;
        private set => SetProperty(ref _elapsed, value);
    }

    public string Distance
    {
        get => _distance;
        private set => SetProperty(ref _distance, value);
    }

    public string HeartRate
    {
        get => _heartRate;
        private set => SetProperty(ref _heartRate, value);
    }

    public string Cadence
    {
        get => _cadence;
        private set => SetProperty(ref _cadence, value);
    }

    public string Pace
    {
        get => _pace;
        private set => SetProperty(ref _pace, value);
    }

    public string Calories
    {
        get => _calories;
        private set => SetProperty(ref _calories, value);
    }

    public string Steps
    {
        get => _steps;
        private set => SetProperty(ref _steps, value);
    }

    public bool HasSummary
    {
        get => _hasSummary;
        private set => SetProperty(ref _hasSummary, value);
    }

    public string TotalDuration
    {
        get => _totalDuration;
        private set => SetProperty(ref _totalDuration, value);
    }

    public string TotalDistance
    {
        get => _totalDistance;
        private set => SetProperty(ref _totalDistance, value);
    }

    public string TotalCalories
    {
        get => _totalCalories;
        private set => SetProperty(ref _totalCalories, value);
    }

    public string TotalSteps
    {
        get => _totalSteps;
        private set => SetProperty(ref _totalSteps, value);
    }

    public string AvgHeartRate
    {
        get => _avgHeartRate;
        private set => SetProperty(ref _avgHeartRate, value);
    }

    public string AvgCadence
    {
        get => _avgCadence;
        private set => SetProperty(ref _avgCadence, value);
    }

    [RelayCommand]
    private async Task Scan()
    {
        if (IsScanning) return;

        var permOk = await BlePermissionHelper.RequestAsync();
        if (!permOk)
        {
            ScanResult = "需要蓝牙权限才能连接手表";
            return;
        }

        IsScanning = true;
        ScanResult = "正在扫描...";
        ConnectionStatus = "扫描中";

        try
        {
            await _bleService.StartScanAsync(15);
            if (string.IsNullOrEmpty(ScanResult) || ScanResult == "正在扫描...")
                ScanResult = "未找到手表，请确保手表在附近且已开机";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void OnDeviceFound(string address)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ScanResult = $"已找到: {address}";
            ConnectionStatus = "正在连接...";
            IsConnecting = true;
            _ = ConnectToDevice(address);
        });
    }

    private static readonly byte[] PreSharedKey = "ae6c9ef31dff140f6abac2112c44abee"
        .Select((c, i) => (c, i))
        .GroupBy(x => x.i / 2)
        .Select(g => Convert.ToByte(new string(g.Select(x => x.c).ToArray()), 16))
        .ToArray();

    private async Task ConnectToDevice(string address)
    {
        try
        {
            await _bleService.ConnectAsync(address);

            ConnectionStatus = "正在认证...";
            var authOk = await _bleService.AuthenticateAsync(PreSharedKey);
            if (!authOk)
            {
                ConnectionStatus = "认证失败，请确保已配对手表";
                IsConnecting = false;
                return;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Connection failed");
            ConnectionStatus = $"连接失败: {ex.Message}";
            IsConnecting = false;
        }
    }

    private void OnConnectionChanged(bool connected)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsConnecting = false;
            if (connected)
            {
                ConnectionStatus = "已连接";
                DeviceName = "小米手表 S5";
            }
            else
            {
                ConnectionStatus = "未连接";
                DeviceName = "";
            }
        });
    }

    [RelayCommand]
    private async Task StartWorkout()
    {
        if (!_bleService.IsConnected)
        {
            ConnectionStatus = "请先连接手表";
            return;
        }

        _receivedData.Clear();
        HasSummary = false;
        ResetDisplay();

        var ok = await _bleService.StartWorkoutAsync(WatchConstants.SPORT_KIND_OUTDOOR);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ConnectionStatus = ok ? "跑步进行中..." : "启动失败";
        });
    }

    [RelayCommand]
    private async Task StopWorkout()
    {
        await _bleService.StopWorkoutAsync();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ConnectionStatus = "已停止";
            ComputeSummary();
        });
    }

    private void OnSportInfoReceived(WatchSportInfo info)
    {
        _receivedData.Add(info);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Elapsed = info.Elapsed.ToString(@"hh\:mm\:ss");
            Distance = info.DistanceKm.ToString("F2");
            HeartRate = info.HeartRate > 0 ? info.HeartRate.ToString() : "--";
            Cadence = info.StepFrequency > 0 ? info.StepFrequency.ToString() : "--";
            Pace = info.PaceText;
            Calories = info.Calories > 0 ? info.Calories.ToString() : "0";
            Steps = info.Steps > 0 ? info.Steps.ToString() : "0";
        });
    }

    private void ComputeSummary()
    {
        if (_receivedData.Count == 0) return;

        var last = _receivedData[^1];

        TotalDuration = last.Elapsed.ToString(@"hh\:mm\:ss");
        TotalDistance = last.DistanceKm.ToString("F2") + " km";
        TotalCalories = last.Calories.ToString();
        TotalSteps = last.Steps.ToString();

        var avgHr = _receivedData.Where(d => d.HeartRate > 0).Select(d => d.HeartRate);
        AvgHeartRate = avgHr.Any() ? $"{(int)avgHr.Average()}" : "--";

        var avgCad = _receivedData.Where(d => d.StepFrequency > 0).Select(d => d.StepFrequency);
        AvgCadence = avgCad.Any() ? $"{(int)avgCad.Average()} spm" : "--";

        HasSummary = true;
    }

    [RelayCommand]
    private async Task Disconnect()
    {
        await _bleService.DisconnectAsync();
    }

    [RelayCommand]
    private async Task ReadFromDb()
    {
        Logger.LogInformation("[DB] Initializing MiHealth data reader...");
        ConnectionStatus = "正在连接 DB...";

        try
        {
            var ok = await _dbReader.InitializeAsync();
            if (!ok)
            {
                ConnectionStatus = "未找到 MiHealth 数据库";
                ScanResult = "需要 root 权限或 MiHealth 未安装";
                return;
            }

            ConnectionStatus = "DB 已连接，正在读取运动记录...";
            var reports = await _dbReader.GetRecentWorkoutsAsync(10);

            if (reports.Count == 0)
            {
                ConnectionStatus = "数据库无运动记录";
                ScanResult = "未找到运动数据";
                return;
            }

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < reports.Count; i++)
            {
                var r = reports[i];
                var dt = DateTimeOffset.FromUnixTimeMilliseconds(r.Time / 1000).LocalDateTime;
                sb.AppendLine($"{i + 1}. [{r.Category}] {dt:yyyy-MM-dd HH:mm}  key={r.Key}");
                if (r.Value?.Length > 0)
                    sb.AppendLine($"   value: {r.Value[..Math.Min(r.Value.Length, 120)]}");
            }
            ScanResult = sb.ToString();
            Logger.LogInformation("[DB] Read {Count} records", reports.Count);
            ConnectionStatus = $"DB: {reports.Count} 条记录";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[DB] Error reading MiHealth database");
            ConnectionStatus = $"DB 错误: {ex.Message}";
            ScanResult = ex.ToString();
        }
    }

    private void ResetDisplay()
    {
        Elapsed = "00:00";
        Distance = "0.00";
        HeartRate = "--";
        Cadence = "--";
        Pace = "--:--";
        Calories = "0";
        Steps = "0";
    }
}
