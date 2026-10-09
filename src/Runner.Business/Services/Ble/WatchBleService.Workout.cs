using Runner.Business.Models.WatchBle;

namespace Runner.Business.Services.Ble;

public partial class WatchBleService
{
    private TaskCompletionSource<byte[]?> _pendingResponse = new();
    private bool _isWorkoutActive;

    public bool IsWorkoutActive => _isWorkoutActive;

    public async Task<bool> AuthenticateAsync(byte[] preSharedKey)
    {
        if (!IsConnected) { Log("Not connected"); return false; }

        Log($"Sending probe write to FDAB control ({_controlChar?.Uuid})...");

        // Try writing a simple command: 0x01 = query version
        var probe = new byte[] { 0x01 };
        var result = await SendCommandAsync(probe);
        if (result != null)
            Log($"Probe response: {BitConverter.ToString(result)}");
        else
            Log("No response from probe write");

        return true;
    }

    public async Task<bool> StartWorkoutAsync(byte sportKind = WatchConstants.SPORT_KIND_OUTDOOR)
    {
        if (!IsConnected || _controlChar == null) return false;

        _isWorkoutActive = false;

        var versionOk = await QueryVersionAsync();
        if (!versionOk) Log("Version query failed, continuing anyway");

        var configOk = await SendConfigAsync(sportKind);
        if (!configOk) { Log("Config failed"); return false; }

        var startOk = await SendSportCommandAsync(SportCommand.Start);
        if (!startOk) { Log("Start command failed"); return false; }

        _isWorkoutActive = true;
        Log($"Workout started (sportKind={sportKind})");
        return true;
    }

    public async Task StopWorkoutAsync()
    {
        if (!IsConnected || _controlChar == null) return;
        await SendSportCommandAsync(SportCommand.Stop);
        _isWorkoutActive = false;
        Log("Workout stopped");
    }

    public async Task PauseWorkoutAsync()
    {
        if (!IsConnected || _controlChar == null) return;
        await SendSportCommandAsync(SportCommand.Pause);
        Log("Workout paused");
    }

    public async Task ResumeWorkoutAsync()
    {
        if (!IsConnected || _controlChar == null) return;
        await SendSportCommandAsync(SportCommand.Resume);
        Log("Workout resumed");
    }

    private async Task<bool> QueryVersionAsync()
    {
        var response = await SendCommandAsync(new byte[] { 0x01 });
        return response != null && response.Length > 0;
    }

    private async Task<bool> SendConfigAsync(byte sportKind)
    {
        var response = await SendCommandAsync(new byte[] { 0x02, sportKind, 0x01 });
        return response != null && response.Length > 0 && response[0] == 0x01;
    }

    private async Task<bool> SendSportCommandAsync(SportCommand command)
    {
        byte cmdByte = command switch
        {
            SportCommand.Start => 0x01,
            SportCommand.Pause => 0x02,
            SportCommand.Resume => 0x03,
            SportCommand.Stop => 0x04,
            _ => 0x00
        };

        var response = await SendCommandAsync(new byte[] { 0x03, cmdByte, 0, 0, 0, 0 });
        return response != null && response.Length > 0 && response[0] == 0x01;
    }

    private void HandleControlResponse(byte[] value)
    {
        var tcs = _pendingResponse;
        if (!tcs.Task.IsCompleted)
        {
            var parsed = ParseResponse(value);
            tcs.TrySetResult(parsed);
        }
    }

    private static byte[]? ParseResponse(byte[] raw)
    {
        if (raw.Length == 0) return null;
        if (raw[0] == 0x10)
        {
            int headerLen = 4;
            if (raw.Length <= headerLen) return null;
            var payload = new byte[raw.Length - headerLen];
            Array.Copy(raw, headerLen, payload, 0, payload.Length);
            return payload;
        }
        return raw;
    }

    private void HandleSportData(byte[] value)
    {
        if (value.Length <= 2 || value[0] != 0x04) return;

        var payloadLen = value.Length - 2;
        var payload = new byte[payloadLen];
        Array.Copy(value, 2, payload, 0, payloadLen);
        var sportInfo = WatchSportInfo.Parse(payload);
        OnSportInfoReceived?.Invoke(sportInfo);
    }
}
