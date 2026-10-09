using System.Runtime.InteropServices;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.OS;
using Microsoft.Extensions.Logging;
using Runner.Business.Models.WatchBle;
using JavaUuid = Java.Util.UUID;

namespace Runner.Business.Services.Ble;

public partial class WatchBleService : IWatchBleService
{
    private readonly ILogger<WatchBleService> _logger;
    private BluetoothAdapter? _bluetoothAdapter;
    private BluetoothLeScanner? _scanner;
    private BluetoothGatt? _gatt;
    private BluetoothGattService? _miliService;
    private BluetoothGattCharacteristic? _controlChar;
    private BluetoothGattCharacteristic? _dataChar;

    private BluetoothDevice? _targetDevice;
    private CancellationTokenSource? _scanCts;
    private BleScanCallback? _scanCallback;
    private BleGattCallback? _gattCallback;

    private TaskCompletionSource<byte[]?> _pendingResponse = new();
    private TaskCompletionSource<bool>? _connectionTcs;
    private TaskCompletionSource<bool>? _mtuTcs;
    private TaskCompletionSource<bool>? _serviceDiscoveredTcs;
    private TaskCompletionSource<byte[]?>? _pendingRead;
    private bool _isWorkoutActive;
    private int _packetIndex = -1;
    private MemoryStream? _dataStream;

    public bool IsConnected => _gatt != null && _gatt.Services?.Count > 0;
    public bool IsWorkoutActive => _isWorkoutActive;

    public event Action<string>? OnLog;
    public event Action<bool>? OnConnectionChanged;
    public event Action<WatchSportInfo>? OnSportInfoReceived;
    public event Action<string>? OnDeviceFound;

    private const string TAG = "[WatchBleService]";

    private static JavaUuid ToUuid(Guid guid) => JavaUuid.FromString(guid.ToString());

    public WatchBleService(ILogger<WatchBleService> logger)
    {
        _logger = logger;
    }

    public async Task StartScanAsync(int timeoutSeconds = 10)
    {
        var context = Android.App.Application.Context;
        var bluetoothManager = (BluetoothManager?)context.GetSystemService(Context.BluetoothService);
        _bluetoothAdapter = bluetoothManager?.Adapter;

        if (_bluetoothAdapter == null) { Log("Bluetooth not supported"); return; }
        if (!_bluetoothAdapter.IsEnabled) { Log("Bluetooth is disabled"); return; }

        // Check already bonded devices first
        foreach (var device in _bluetoothAdapter.BondedDevices ?? Array.Empty<BluetoothDevice>())
        {
            if (device.Name?.Contains(WatchConstants.WATCH_NAME_PREFIX, StringComparison.OrdinalIgnoreCase) == true)
            {
                Log($"Found bonded device: {device.Name} [{device.Address}]");
                OnDeviceFound?.Invoke(device.Address);
                return;
            }
        }

        _scanner = _bluetoothAdapter.BluetoothLeScanner;
        if (_scanner == null) { Log("BLE scanner not available"); return; }

        _scanCts = new CancellationTokenSource();
        _scanCallback = new BleScanCallback(this);
        _scanner.StartScan(_scanCallback);

        Log($"Scanning for {WatchConstants.WATCH_NAME_PREFIX}... (timeout: {timeoutSeconds}s)");

        try
        {
            await Task.Delay(timeoutSeconds * 1000, _scanCts.Token);
        }
        catch (TaskCanceledException) { }

        _scanner.StopScan(_scanCallback);
        Log("Scan stopped");
    }

    public async Task ConnectAsync(string deviceAddress)
    {
        if (_bluetoothAdapter == null) { Log("Bluetooth not initialized"); return; }

        var device = _bluetoothAdapter.GetRemoteDevice(deviceAddress);
        if (device == null) { Log($"Device not found: {deviceAddress}"); return; }

        _targetDevice = device;
        var context = Android.App.Application.Context;

        // Clean up any existing GATT connection first
        if (_gatt != null)
        {
            _gatt.Disconnect();
            _gatt.Close();
            _gatt = null;
            _gattCallback?.Dispose();
            _gattCallback = null;
            await Task.Delay(500);
        }

        _connectionTcs = new TaskCompletionSource<bool>();

        // Try LE transport first (needed for FDAA/MiConnect service)
        Log($"Connecting to {device.Name} ({deviceAddress}) via LE...");
        _gattCallback = new BleGattCallback(this);
        _gatt = device.ConnectGatt(context, false, _gattCallback, BluetoothTransports.Le);

        var connected = await AwaitConnectionAsync(8000);

        // If LE transport failed, fall back to auto
        if (!connected)
        {
            Log("LE transport failed, retrying with auto transport...");
            if (_gatt != null) { _gatt.Disconnect(); _gatt.Close(); _gatt = null; }
            _gattCallback?.Dispose();

            _connectionTcs = new TaskCompletionSource<bool>();
            _gattCallback = new BleGattCallback(this);
            _gatt = device.ConnectGatt(context, false, _gattCallback);
            connected = await AwaitConnectionAsync(5000);
        }

        if (!connected)
        {
            Log($"Failed to connect to {deviceAddress}");
            _gatt = null;
        }
        else
        {
            Log($"Connected: {_targetDevice?.Name}");
        }
    }

    private async Task<bool> AwaitConnectionAsync(int timeoutMs)
    {
        if (_connectionTcs == null) return false;

        var timeout = Task.Delay(timeoutMs);
        var done = await Task.WhenAny(_connectionTcs.Task, timeout);
        if (done == timeout) return false;

        _serviceDiscoveredTcs = new TaskCompletionSource<bool>();
        var svcTimeout = Task.Delay(8000);
        var svcDone = await Task.WhenAny(_serviceDiscoveredTcs.Task, svcTimeout);
        return svcDone != svcTimeout && _gatt?.Services?.Count > 0;
    }

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

    public Task DisconnectAsync()
    {
        if (_gatt != null)
        {
            _gatt.Disconnect();
            _gatt.Close();
        }
        _gatt = null;
        _gattCallback?.Dispose();
        _gattCallback = null;
        _miliService = null;
        _controlChar = null;
        _dataChar = null;
        _isWorkoutActive = false;
        _packetIndex = -1;
        _pendingResponse = new TaskCompletionSource<byte[]?>();
        OnConnectionChanged?.Invoke(false);
        Log("Disconnected");
        return Task.CompletedTask;
    }

    private void OnGattConnected()
    {
        Log("Connected, waiting for bonded device service changed...");
        // Xiaomi waits 1600ms for bonded devices to handle Service Changed indication
        Task.Run(async () =>
        {
            await Task.Delay(1600);
            if (_gatt != null)
            {
                Log("Requesting MTU...");
                _gatt.RequestMtu(512);
            }
        });
    }

    private void OnServicesDiscovered()
    {
        if (_gatt == null) return;

        foreach (var svc in _gatt.Services ?? Enumerable.Empty<BluetoothGattService>())
        {
            Log($"  Service: {svc.Uuid} (type={svc.Type})");
            foreach (var chr in svc.Characteristics ?? Enumerable.Empty<BluetoothGattCharacteristic>())
            {
                var props = chr.Properties;
                Log($"    Char: {chr.Uuid} (props={props})");

                if (chr.Uuid.Equals(ToUuid(WatchConstants.CHAR_FDAB_CONTROL)))
                {
                    _controlChar = chr;
                    Log($"    -> FDAB control");
                }
                if (chr.Uuid.Equals(ToUuid(WatchConstants.CHAR_FDAB_DATA)))
                {
                    _dataChar = chr;
                    Log($"    -> FDAB data");
                }
            }
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
            await TryReadAllCharacteristicsAsync();
        });

        _serviceDiscoveredTcs?.TrySetResult(true);
        OnConnectionChanged?.Invoke(true);
        Log("Services discovered, ready");
    }

    private async Task TryReadAllCharacteristicsAsync()
    {
        if (_gatt == null) return;

        foreach (var svc in _gatt.Services ?? Enumerable.Empty<BluetoothGattService>())
        {
            foreach (var chr in svc.Characteristics ?? Enumerable.Empty<BluetoothGattCharacteristic>())
            {
                var props = chr.Properties;
                if ((props & GattProperty.Read) != 0)
                {
                    var val = await ReadCharacteristicAsync(chr);
                    if (val != null)
                        Log($"  Read {chr.Uuid}: {BitConverter.ToString(val)}");
                    else
                        Log($"  Read {chr.Uuid}: (read failed)");
                    await Task.Delay(200);
                }
                if ((props & GattProperty.Notify) != 0 || (props & GattProperty.Indicate) != 0)
                {
                    EnableNotification(chr);
                    Log($"  Notify enabled: {chr.Uuid}");
                }
            }
        }
    }

    private async Task<byte[]?> ReadCharacteristicAsync(BluetoothGattCharacteristic chr)
    {
        if (_gatt == null) return null;
        var tcs = new TaskCompletionSource<byte[]?>();
        _pendingRead = tcs;
        if (!_gatt.ReadCharacteristic(chr))
        {
            _pendingRead = null;
            return null;
        }
        var timeout = Task.Delay(5000);
        var done = await Task.WhenAny(tcs.Task, timeout);
        _pendingRead = null;
        return done == timeout ? null : tcs.Task.Result;
    }

    private void EnableNotification(BluetoothGattCharacteristic characteristic)
    {
        if (_gatt == null) return;

        _gatt.SetCharacteristicNotification(characteristic, true);
        var descriptor = characteristic.GetDescriptor(ToUuid(WatchConstants.CHAR_CCC));
        if (descriptor != null)
        {
            descriptor.SetValue(BluetoothGattDescriptor.EnableNotificationValue?.ToArray());
            _gatt.WriteDescriptor(descriptor);
        }
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
        _packetIndex = -1;
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

    private async Task<byte[]?> SendCommandAsync(byte[] command)
    {
        if (_gatt == null || _controlChar == null) return null;

        var tcs = new TaskCompletionSource<byte[]?>();
        var timeoutCts = new CancellationTokenSource(5000);
        timeoutCts.Token.Register(() => tcs.TrySetResult(null));
        _pendingResponse = tcs;

        _controlChar.SetValue(command);
        _controlChar.WriteType = GattWriteType.Default;

        var success = _gatt.WriteCharacteristic(_controlChar);
        if (!success) { Log("Write failed"); return null; }

        return await tcs.Task;
    }

    private void HandleNotification(BluetoothGattCharacteristic characteristic, byte[] value)
    {
        Log($"Notification: {characteristic.Uuid} len={value.Length} data={BitConverter.ToString(value)}");

        if (characteristic.Uuid.Equals(ToUuid(WatchConstants.CHAR_CONTROL_POINT)))
            HandleControlResponse(value);
        else if (characteristic.Uuid.Equals(ToUuid(WatchConstants.CHAR_DATA)))
            HandleSportData(value);
    }

    private void HandleControlResponse(byte[] value)
    {
        var tcs = _pendingResponse;
        if (tcs != null && !tcs.Task.IsCompleted)
        {
            var parsed = ParseResponse(value);
            tcs.TrySetResult(parsed);
        }
    }

    private static byte[]? ParseResponse(byte[] raw)
    {
        if (raw == null || raw.Length == 0) return null;
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
        if (value.Length > 1)
        {
            int type = value[0] & 0xFF;
            if (type == 0x04 && value.Length > 2)
            {
                var payloadLen = value.Length - 2;
                var payload = new byte[payloadLen];
                Array.Copy(value, 2, payload, 0, payloadLen);
                var sportInfo = WatchSportInfo.Parse(payload);
                OnSportInfoReceived?.Invoke(sportInfo);
            }
        }
    }

    private void Log(string message)
    {
        _logger?.LogInformation("{Tag} {Message}", TAG, message);
        System.Diagnostics.Debug.WriteLine($"{TAG} {message}");
        OnLog?.Invoke(message);
    }
}
