using Android.Bluetooth;
using Runner.Business.Models.WatchBle;
using JavaUuid = Java.Util.UUID;

namespace Runner.Business.Services.Ble;

public partial class WatchBleService
{
    private BluetoothGattCharacteristic? _controlChar;
    private TaskCompletionSource<byte[]?>? _pendingRead;

    private static JavaUuid ToUuid(Guid guid) => JavaUuid.FromString(guid.ToString())
        ?? throw new InvalidOperationException($"Could not create Bluetooth UUID: {guid}");

    private void OnServicesDiscovered()
    {
        var gatt = _gatt;
        if (gatt == null) return;

        foreach (var svc in gatt.Services ?? Enumerable.Empty<BluetoothGattService>())
        {
            Log($"  Service: {svc.Uuid} (type={svc.Type})");
            foreach (var chr in svc.Characteristics ?? Enumerable.Empty<BluetoothGattCharacteristic>())
                OnCharacteristicDiscovered(chr);
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

    private void OnCharacteristicDiscovered(BluetoothGattCharacteristic characteristic)
    {
        var uuid = characteristic.Uuid;
        Log($"    Char: {uuid} (props={characteristic.Properties})");
        if (uuid == null) return;

        if (uuid.Equals(ToUuid(WatchConstants.CHAR_FDAB_CONTROL)))
        {
            _controlChar = characteristic;
            Log("    -> FDAB control");
        }
        if (uuid.Equals(ToUuid(WatchConstants.CHAR_FDAB_DATA)))
            Log("    -> FDAB data");
    }

    private async Task TryReadAllCharacteristicsAsync()
    {
        var gatt = _gatt;
        if (gatt == null) return;

        var characteristics = (gatt.Services ?? Enumerable.Empty<BluetoothGattService>())
            .SelectMany(service => service.Characteristics ?? Enumerable.Empty<BluetoothGattCharacteristic>());
        foreach (var chr in characteristics)
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

    private async Task<byte[]?> ReadCharacteristicAsync(BluetoothGattCharacteristic chr)
    {
        var gatt = _gatt;
        if (gatt == null) return null;

        var tcs = new TaskCompletionSource<byte[]?>();
        _pendingRead = tcs;
        if (!gatt.ReadCharacteristic(chr))
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
        var gatt = _gatt;
        if (gatt == null) return;

        gatt.SetCharacteristicNotification(characteristic, true);
        var descriptor = characteristic.GetDescriptor(ToUuid(WatchConstants.CHAR_CCC));
        if (descriptor != null)
        {
            descriptor.SetValue(BluetoothGattDescriptor.EnableNotificationValue?.ToArray());
            gatt.WriteDescriptor(descriptor);
        }
    }

    private async Task<byte[]?> SendCommandAsync(byte[] command)
    {
        var gatt = _gatt;
        var controlChar = _controlChar;
        if (gatt == null || controlChar == null) return null;

        var tcs = new TaskCompletionSource<byte[]?>();
        using var timeoutCts = new CancellationTokenSource(5000);
        using var registration = timeoutCts.Token.Register(() => tcs.TrySetResult(null));
        _pendingResponse = tcs;

        controlChar.SetValue(command);
        controlChar.WriteType = GattWriteType.Default;

        var success = gatt.WriteCharacteristic(controlChar);
        if (!success) { Log("Write failed"); return null; }

        return await tcs.Task;
    }

    private void HandleNotification(BluetoothGattCharacteristic characteristic, byte[] value)
    {
        var uuid = characteristic.Uuid;
        Log($"Notification: {uuid} len={value.Length} data={BitConverter.ToString(value)}");
        if (uuid == null) return;

        if (uuid.Equals(ToUuid(WatchConstants.CHAR_CONTROL_POINT)))
            HandleControlResponse(value);
        else if (uuid.Equals(ToUuid(WatchConstants.CHAR_DATA)))
            HandleSportData(value);
    }
}
