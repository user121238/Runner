using Runner.Business.Models.WatchBle;

namespace Runner.Business.Services.Ble;

public interface IWatchBleService
{
    bool IsConnected { get; }
    bool IsWorkoutActive { get; }

    event Action<string> OnLog;
    event Action<bool> OnConnectionChanged;
    event Action<WatchSportInfo> OnSportInfoReceived;
    event Action<string> OnDeviceFound;

    Task StartScanAsync(int timeoutSeconds = 10);
    Task ConnectAsync(string deviceAddress);
    Task DisconnectAsync();

    Task<bool> AuthenticateAsync(byte[] preSharedKey);
    Task<bool> StartWorkoutAsync(byte sportKind = WatchConstants.SPORT_KIND_OUTDOOR);
    Task StopWorkoutAsync();
    Task PauseWorkoutAsync();
    Task ResumeWorkoutAsync();
}
