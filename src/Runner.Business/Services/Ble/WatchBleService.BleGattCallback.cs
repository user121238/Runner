using Android.Bluetooth;

namespace Runner.Business.Services.Ble;

public partial class WatchBleService
{
    private class BleGattCallback : BluetoothGattCallback
    {
        private readonly WatchBleService _service;
        public BleGattCallback(WatchBleService service) => _service = service;

        public override void OnConnectionStateChange(BluetoothGatt? gatt, GattStatus status, ProfileState newState)
        {
            if (newState == ProfileState.Connected)
            {
                _service._connectionTcs?.TrySetResult(true);
                _service.OnGattConnected();
            }
            else if (newState == ProfileState.Disconnected)
            {
                _service._connectionTcs?.TrySetResult(false);
                _service.OnConnectionChanged?.Invoke(false);
            }
        }

        public override void OnServicesDiscovered(BluetoothGatt? gatt, GattStatus status)
        {
            if (status == GattStatus.Success)
                _service.OnServicesDiscovered();
            else
                _service.Log($"Service discovery failed: {status}");
        }

        public override void OnCharacteristicChanged(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic, byte[]? value)
        {
            if (characteristic != null && value != null)
                _service.HandleNotification(characteristic, value);
        }

        public override void OnCharacteristicWrite(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic, GattStatus status)
        {
            if (status != GattStatus.Success)
                _service.Log($"Write failed: {status}");
        }

        public override void OnCharacteristicRead(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, byte[] value, GattStatus status)
        {
            _service.Log($"Read {characteristic.Uuid}: {status} len={value?.Length}");
            _service._pendingRead?.TrySetResult(value);
        }

        public override void OnDescriptorWrite(BluetoothGatt? gatt, BluetoothGattDescriptor? descriptor, GattStatus status)
        {
            if (status != GattStatus.Success)
                _service.Log($"Descriptor write failed: {status}");
        }

        public override void OnMtuChanged(BluetoothGatt? gatt, int mtu, GattStatus status)
        {
            _service._mtuTcs?.TrySetResult(true);
            _service.Log($"MTU changed: {mtu}");
            _service._gatt?.DiscoverServices();
        }
    }
}
