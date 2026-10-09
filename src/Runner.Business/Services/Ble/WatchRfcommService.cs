using Android.Bluetooth;
using Android.Content;
using Android.Runtime;
using Java.Interop;
using Microsoft.Extensions.Logging;
using Runner.Business.Models.WatchBle;
using JavaUuid = Java.Util.UUID;

namespace Runner.Business.Services.Ble;

public class WatchRfcommService : IWatchBleService, IDisposable
{
    private readonly ILogger<WatchRfcommService> _logger;
    private BluetoothDevice? _device;
    private BluetoothSocket? _socket;
    private Stream? _inputStream;
    private Stream? _outputStream;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    private TaskCompletionSource<byte[]?>? _responseTcs;
    private readonly object _lock = new();
    private int _sessionCounter;
    private bool _isWorkoutActive;
    private byte[]? _recvBuffer;
    private int _recvOffset;

    public event Action<string>? OnLog;
    public event Action<bool>? OnConnectionChanged;
    public event Action<WatchSportInfo>? OnSportInfoReceived;
    public event Action<string>? OnDeviceFound;

    public bool IsConnected => _socket?.IsConnected ?? false;
    public bool IsWorkoutActive => _isWorkoutActive;

    private const string TAG = "[WatchRfcomm]";
    private static readonly JavaUuid SPP_UUID = JavaUuid.FromString("00001101-0000-1000-8000-00805F9B34FB");

    // ---- ba dc fe auth (11 bytes, NN = session counter at [8]) ----
    private static readonly byte[] BADCFE_AUTH_INIT =
        { 0xba, 0xdc, 0xfe, 0x00, 0xc0, 0x03, 0x00, 0x00, 0x00, 0x00, 0xef };

    private static readonly byte[] BADCFE_AUTH_RESP =
        { 0xba, 0xdc, 0xfe, 0x00, 0x00, 0x06, 0x00, 0x01, 0x02, 0x00, 0x03, 0x01, 0x37, 0xef };

    // ---- Replay: a5 a5 02 00 init (captured from BTSnoop, fixed across sessions) ----
    private static readonly byte[] A5A5_INIT_SEND =
    {
        0xa5, 0xa5, 0x02, 0x00, 0x16, 0x00,
        0x1d, 0x4d, 0x01, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x02,
        0x02, 0x00, 0x00, 0xfc, 0x03, 0x02, 0x00, 0x20, 0x00, 0x04,
        0x02, 0x00, 0x10, 0x27
    };

    private static readonly byte[] A5A5_ACK_ZERO =
        { 0xa5, 0xa5, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00 };

    // ---- Replay: entire Session 1 captured exchange ----
    private static readonly byte[] A5A5_AUTH_CHALLENGE_SESSION1 =
    {
        0xa5, 0xa5, 0x03, 0x00, 0x1d, 0x00,
        0x29, 0x42, 0x01, 0x01, 0x08, 0x01, 0x10, 0x1a, 0x1a, 0x15,
        0xf2, 0x01, 0x12, 0x0a, 0x10, 0x1b, 0xb7, 0x53, 0x83, 0x63,
        0x95, 0xc8, 0x8c, 0xf7, 0x2f, 0x4f, 0x68, 0x58, 0xef, 0x73,
        0x35
    };

    private static readonly byte[] A5A5_AUTH_RESPOND_SESSION1 =
    {
        0xa5, 0xa5, 0x03, 0x01, 0x1d, 0x00,
        0xc1, 0x1f, 0x01, 0x01, 0x08, 0x01, 0x10, 0x1a, 0x1a, 0x15,
        0xf2, 0x01, 0x12, 0x0a, 0x10, 0x0a, 0x96, 0xa0, 0x4c, 0xd6,
        0x47, 0x57, 0xcf, 0x3c, 0x1b, 0xb6, 0xed, 0x0e, 0x8f, 0x1c,
        0x4d
    };

    private static readonly byte[] A5A5_CONFIG_1_SESSION1 =
    {
        0xa5, 0xa5, 0x03, 0x02, 0x1d, 0x00,
        0x35, 0xff, 0x01, 0x01, 0x08, 0x01, 0x10, 0x1a, 0x1a, 0x15,
        0xf2, 0x01, 0x12, 0x0a, 0x10, 0x71, 0xbf, 0x6e, 0x87, 0x2e,
        0x80, 0x1e, 0x72, 0x24, 0x61, 0x16, 0xde, 0xce, 0x22, 0x47,
        0xfd
    };

    private static readonly byte[] A5A5_CONFIG_2_SESSION1 =
    {
        0xa5, 0xa5, 0x03, 0x03, 0x1d, 0x00,
        0x79, 0x61, 0x01, 0x01, 0x08, 0x01, 0x10, 0x1a, 0x1a, 0x15,
        0xf2, 0x01, 0x12, 0x0a, 0x10, 0x38, 0x22, 0xc2, 0xe1, 0xa4,
        0x2e, 0xae, 0xb9, 0x1a, 0x03, 0xfd, 0xd8, 0x55, 0xae, 0x1d,
        0xd6
    };

    private static readonly byte[] A5A5_CONFIG_3_SESSION1 =
    {
        0xa5, 0xa5, 0x03, 0x04, 0x1d, 0x00,
        0x0d, 0xdd, 0x01, 0x01, 0x08, 0x01, 0x10, 0x1a, 0x1a, 0x15,
        0xf2, 0x01, 0x12, 0x0a, 0x10, 0x2d, 0x9e, 0x28, 0xdd, 0x6a,
        0x7a, 0xe5, 0xfa, 0xa0, 0x74, 0x21, 0xe9, 0x28, 0x4c, 0x08,
        0x7d
    };

    // a5 a5 03 05 1e 00 — start workout cmd part 1
    private static readonly byte[] A5A5_START_1_SESSION1 =
    {
        0xa5, 0xa5, 0x03, 0x05, 0x1e, 0x00,
        0x83, 0x39, 0x01, 0x01, 0x08, 0x01, 0x10, 0x11, 0x1a, 0x16,
        0x5a, 0x14, 0x08, 0x00, 0x12, 0x10, 0x85, 0xb0, 0xae, 0x84,
        0x29, 0xa3, 0xe8, 0x5b, 0xf1, 0x14, 0x2a, 0xf4, 0xc9, 0x33,
        0x2b, 0x5a
    };

    // a5 a5 03 05 67 00 — start workout cmd part 2 (watch sends, then we need to config further)
    // This is the big 103-byte frame with device info

    public WatchRfcommService(ILogger<WatchRfcommService> logger)
    {
        _logger = logger;
        _recvBuffer = new byte[4096];
    }

    public async Task StartScanAsync(int timeoutSeconds = 10)
    {
        var context = Android.App.Application.Context;
        var bluetoothManager = (BluetoothManager?)context.GetSystemService(Context.BluetoothService);
        var adapter = bluetoothManager?.Adapter;
        if (adapter == null) { Log("Bluetooth not supported"); return; }
        if (!adapter.IsEnabled) { Log("Bluetooth is disabled"); return; }

        foreach (var device in adapter.BondedDevices ?? Array.Empty<BluetoothDevice>())
        {
            if (device.Name?.Contains(WatchConstants.WATCH_NAME_PREFIX, StringComparison.OrdinalIgnoreCase) == true)
            {
                Log($"Found bonded device: {device.Name} [{device.Address}]");
                OnDeviceFound?.Invoke(device.Address);
                return;
            }
        }
        Log($"No bonded {WatchConstants.WATCH_NAME_PREFIX} found");
    }

    public async Task ConnectAsync(string deviceAddress)
    {
        var context = Android.App.Application.Context;
        var bluetoothManager = (BluetoothManager?)context.GetSystemService(Context.BluetoothService);
        var adapter = bluetoothManager?.Adapter;

        if (adapter == null)
            throw new InvalidOperationException("Bluetooth not available or permission denied (BLUETOOTH_CONNECT)");

        if (!adapter.IsEnabled)
            throw new InvalidOperationException("Bluetooth is disabled");

        var remoteDevice = adapter.GetRemoteDevice(deviceAddress);
        if (remoteDevice == null)
            throw new InvalidOperationException($"Device not found: {deviceAddress}");

        await CleanupAsync();

        _device = remoteDevice;

        Log($"Connecting to {_device.Name} ({deviceAddress}) via RFCOMM SPP...");

        adapter.CancelDiscovery();

        _socket = CreateRfcommSocket(_device, 5)
            ?? _device.CreateRfcommSocketToServiceRecord(SPP_UUID);

        try
        {
            await Task.Run(() => _socket.Connect());
        }
        catch (Exception ex)
        {
            await CleanupAsync();
            OnConnectionChanged?.Invoke(false);
            throw new InvalidOperationException($"RFCOMM connect failed: {ex.Message}", ex);
        }

        _inputStream = _socket.InputStream;
        _outputStream = _socket.OutputStream;
        Log("RFCOMM socket connected");

        var handshakeOk = await DoHandshakeAsync();
        if (!handshakeOk)
        {
            await DisconnectAsync();
            throw new InvalidOperationException("SPP handshake failed - watch rejected auth or protocol mismatch");
        }

        _receiveCts = new CancellationTokenSource();
        _receiveTask = ReceiveLoopAsync(_receiveCts.Token);

        OnConnectionChanged?.Invoke(true);
        Log("Connection established");
    }

    private async Task<bool> DoHandshakeAsync()
    {
        if (_inputStream == null || _outputStream == null) return false;

        var ct = new CancellationTokenSource(15000).Token;

        try
        {
            // Step 1: ba dc fe auth init  [Phone→Watch, 11B]
            var authInit = new byte[11];
            Buffer.BlockCopy(BADCFE_AUTH_INIT, 0, authInit, 0, 11);
            authInit[8] = (byte)(_sessionCounter++);

            Log($"Auth init (session={authInit[8]}): {BitConverter.ToString(authInit)}");
            await WriteRawAsync(authInit, ct);

            // Step 2: ba dc fe auth response  [Watch→Phone, 14B]
            var authResp = await ReadExactAsync(14, ct);
            Log($"Auth resp: {BitConverter.ToString(authResp)}");

            // Step 3: a5 a5 02 00 init  [Phone→Watch, replay]
            Log($"Init: {BitConverter.ToString(A5A5_INIT_SEND)}");
            await WriteRawAsync(A5A5_INIT_SEND, ct);

            // Step 4: a5 a5 02 00 echo  [Watch→Phone, no ACK]
            var initEcho = await ReadA5FrameAsync(ct);
            Log($"Init echo: type={initEcho.type:X2} sub={initEcho.subtype:X2}");

            // Step 5+: auth + config exchanges (03 00 → 03 04)
            var exchanges = new[]
            {
                ("auth challenge", A5A5_AUTH_CHALLENGE_SESSION1),
                ("auth respond ", A5A5_AUTH_RESPOND_SESSION1),
                ("config 1     ", A5A5_CONFIG_1_SESSION1),
                ("config 2     ", A5A5_CONFIG_2_SESSION1),
                ("config 3     ", A5A5_CONFIG_3_SESSION1),
            };

            foreach (var (name, data) in exchanges)
            {
                Log($"Sending {name} (03 {data[3]:X2})...");
                var resp = await SendAndReceiveDataAsync(data, ct);
                Log($"  {name} resp: type={resp.type:X2} sub={resp.subtype:X2} len={resp.payload.Length}");
            }

            Log("Handshake completed");
            return true;
        }
        catch (Exception ex)
        {
            Log($"Handshake error: {ex.Message}");
            return false;
        }
    }

    private async Task<(byte type, byte subtype, byte[] payload)> SendAndReceiveDataAsync(byte[] request, CancellationToken ct)
    {
        await WriteRawAsync(request, ct);

        while (true)
        {
            var frame = await ReadA5FrameAsync(ct);

            if (frame.type == 0x03)
            {
                await SendAckAsync(frame.subtype, ct);
                return frame;
            }

            if (frame.type != 0x01)
                Log($"  unexpected frame type={frame.type:X2} sub={frame.subtype:X2}");
        }
    }

    private async Task SendAckAsync(byte subtype, CancellationToken ct)
    {
        var ack = new byte[8] { 0xa5, 0xa5, 0x01, subtype, 0x00, 0x00, 0x00, 0x00 };
        await WriteRawAsync(ack, ct);
    }

    public async Task<bool> AuthenticateAsync(byte[] preSharedKey)
    {
        return true;
    }

    public async Task<bool> StartWorkoutAsync(byte sportKind = WatchConstants.SPORT_KIND_OUTDOOR)
    {
        if (!IsConnected)
        {
            Log("Not connected, cannot start workout");
            return false;
        }

        try
        {
            var ct = new CancellationTokenSource(15000).Token;

            Log("Sending start workout (03 05 step 1)...");
            var resp1 = await SendAndReceiveDataAsync(A5A5_START_1_SESSION1, ct);
            Log($"Start1 resp: type={resp1.type:X2} sub={resp1.subtype:X2} len={resp1.payload.Length}");

            _isWorkoutActive = true;
            Log("Workout started");
            return true;
        }
        catch (Exception ex)
        {
            Log($"Start workout error: {ex.Message}");
            return false;
        }
    }

    public async Task StopWorkoutAsync()
    {
        _isWorkoutActive = false;
        Log("Workout stopped");
        await Task.CompletedTask;
    }

    public Task PauseWorkoutAsync()
    {
        Log("Workout paused");
        return Task.CompletedTask;
    }

    public Task ResumeWorkoutAsync()
    {
        Log("Workout resumed");
        return Task.CompletedTask;
    }

    public async Task DisconnectAsync()
    {
        await CleanupAsync();
        OnConnectionChanged?.Invoke(false);
        Log("Disconnected");
    }

    private static BluetoothSocket? CreateRfcommSocket(BluetoothDevice device, int channel)
    {
        try
        {
            var cls = JNIEnv.GetObjectClass(device.Handle);
            var method = JNIEnv.GetMethodID(cls, "createRfcommSocket", "(I)Landroid/bluetooth/BluetoothSocket;");
            var handle = JNIEnv.CallObjectMethod(device.Handle, method, new JValue(channel));
            JNIEnv.DeleteLocalRef(cls);
            return handle == IntPtr.Zero
                ? null
                : Java.Lang.Object.GetObject<BluetoothSocket>(handle, JniHandleOwnership.TransferLocalRef);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"{TAG} createRfcommSocket({channel}) failed: {ex.Message}");
            return null;
        }
    }

    private async Task CleanupAsync()
    {
        if (_receiveCts != null)
        {
            _receiveCts.Cancel();
            _receiveCts = null;
        }

        try { _socket?.Close(); } catch { }
        try { _socket?.Dispose(); } catch { }
        _socket = null;
        _inputStream = null;
        _outputStream = null;
        _device = null;
        _recvOffset = 0;
        _isWorkoutActive = false;
    }

    // ---- Stream I/O ----

    private async Task WriteRawAsync(byte[] data, CancellationToken ct)
    {
        if (_outputStream == null) throw new InvalidOperationException("Not connected");
        await _outputStream.WriteAsync(data, ct);
        await _outputStream.FlushAsync(ct);
    }

    private async Task<byte[]> ReadExactAsync(int count, CancellationToken ct)
    {
        if (_inputStream == null) throw new InvalidOperationException("Not connected");
        var buffer = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int read = await _inputStream.ReadAsync(buffer.AsMemory(offset, count - offset), ct);
            if (read == 0) throw new EndOfStreamException("Connection closed");
            offset += read;
        }
        return buffer;
    }

    // ---- a5 a5 frame parsing ----
    // Frame format: [a5][a5][type:1][subtype:1][len:2 LE][payload: len bytes]

    private async Task<(byte type, byte subtype, byte[] payload)> ReadA5FrameAsync(CancellationToken ct)
    {
        while (true)
        {
            // Scan buffer for a5 a5 magic
            int magicIdx = FindMagicInBuffer();
            if (magicIdx >= 0)
            {
                // Consume bytes before magic
                if (magicIdx > 0)
                    Array.Copy(_recvBuffer!, magicIdx, _recvBuffer!, 0, _recvOffset - magicIdx);
                _recvOffset -= magicIdx;
                magicIdx = 0;

                if (_recvOffset < 6)
                {
                    // Need header
                    int need = 6 - _recvOffset;
                    int read = await _inputStream!.ReadAsync(_recvBuffer!.AsMemory(_recvOffset, need), ct);
                    if (read == 0) throw new EndOfStreamException("Connection closed");
                    _recvOffset += read;
                    continue;
                }

                byte type = _recvBuffer![2];
                byte subtype = _recvBuffer![3];
                int payloadLen = _recvBuffer![4] | (_recvBuffer![5] << 8);
                int totalFrameLen = 6 + payloadLen;

                if (_recvOffset < totalFrameLen)
                {
                    // Need payload bytes
                    int need = totalFrameLen - _recvOffset;
                    int read = await _inputStream!.ReadAsync(_recvBuffer.AsMemory(_recvOffset, need), ct);
                    if (read == 0) throw new EndOfStreamException("Connection closed");
                    _recvOffset += read;
                    continue;
                }

                var payload = new byte[payloadLen];
                Array.Copy(_recvBuffer, 6, payload, 0, payloadLen);

                // Consume this frame from buffer
                int remaining = _recvOffset - totalFrameLen;
                if (remaining > 0)
                    Array.Copy(_recvBuffer, totalFrameLen, _recvBuffer, 0, remaining);
                _recvOffset = remaining;

                return (type, subtype, payload);
            }

            // No magic found, need more data
            if (_recvOffset >= _recvBuffer!.Length - 10)
            {
                _recvOffset = 0;
            }

            int more = await _inputStream!.ReadAsync(_recvBuffer.AsMemory(_recvOffset, _recvBuffer.Length - _recvOffset), ct);
            if (more == 0) throw new EndOfStreamException("Connection closed");
            _recvOffset += more;
        }
    }

    private int FindMagicInBuffer()
    {
        if (_recvOffset < 2) return -1;
        for (int i = 0; i <= _recvOffset - 2; i++)
        {
            if (_recvBuffer![i] == 0xa5 && _recvBuffer![i + 1] == 0xa5)
                return i;
        }
        return -1;
    }

    // ---- Decode/sport data (from a5 a5 sport frames) ----

    private void DispatchA5Frame(byte type, byte subtype, byte[] payload)
    {
        if (type == 0x01) return;

        if (type == 0x03 && payload.Length > 0)
        {
            // Could be sport data
            try
            {
                var sportInfo = WatchSportInfo.Parse(payload);
                OnSportInfoReceived?.Invoke(sportInfo);
            }
            catch { }
        }
    }

    private void Log(string message)
    {
        _logger?.LogInformation("{Tag} {Message}", TAG, message);
        System.Diagnostics.Debug.WriteLine($"{TAG} {message}");
        OnLog?.Invoke(message);
    }

    public void Dispose()
    {
        CleanupAsync().GetAwaiter().GetResult();
    }

    // ---- Receive loop for ongoing data ----

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var frame = await ReadA5FrameAsync(ct);
                DispatchA5Frame(frame.type, frame.subtype, frame.payload);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log($"Receive loop error: {ex.Message}");
            OnConnectionChanged?.Invoke(false);
        }
    }
}
