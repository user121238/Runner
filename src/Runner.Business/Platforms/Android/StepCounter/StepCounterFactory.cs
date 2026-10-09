using Android.Content;
using Android.Hardware;
using System.Diagnostics;

namespace Runner.Business.Platforms.Android.StepCounter
{
    /// <summary>
    /// 计步器工厂
    /// 根据设备能力选择最佳实现
    /// </summary>
    public static class StepCounterFactory
    {
        /// <summary>
        /// 强制使用加速度传感器（用于测试）
        /// </summary>
        public static bool ForceAccelerometer { get; set; } = false;

        /// <summary>
        /// 创建计步器实例；设备没有任何计步传感器时返回 null，调用方退化为仅节拍器模式。
        /// </summary>
        /// <returns>计步器实例，或 null</returns>
        public static IStepCounter? Create(Context context)
        {
            Debug.WriteLine("[StepCounterFactory] 开始创建计步器");
            Debug.WriteLine($"[StepCounterFactory] 强制使用加速度传感器: {ForceAccelerometer}");

            var sensorManager = (SensorManager?)context.GetSystemService(Context.SensorService);

            if (sensorManager == null)
            {
                Debug.WriteLine("[StepCounterFactory] 错误: 无法获取传感器管理器");
                throw new InvalidOperationException("无法获取传感器管理器");
            }

            Debug.WriteLine("[StepCounterFactory] 传感器管理器获取成功");

            // 列出所有可用传感器
            var allSensors = sensorManager.GetSensorList(SensorType.All);
            Debug.WriteLine($"[StepCounterFactory] 设备共有 {allSensors?.Count ?? 0} 个传感器");
            if (allSensors != null)
            {
                foreach (var sensor in allSensors)
                {
                    Debug.WriteLine($"[StepCounterFactory] - {sensor.Name} (Type: {sensor.Type})");
                }
            }

            // 如果强制使用加速度传感器，直接跳到加速度传感器
            if (!ForceAccelerometer)
            {
                // 优先使用硬件计步传感器
                var stepDetectorSensor = sensorManager.GetDefaultSensor(SensorType.StepDetector, true)
                    ?? sensorManager.GetDefaultSensor(SensorType.StepDetector);
                var stepCounterSensor = sensorManager.GetDefaultSensor(SensorType.StepCounter, true)
                    ?? sensorManager.GetDefaultSensor(SensorType.StepCounter);

                if (stepDetectorSensor != null)
                {
                    Debug.WriteLine($"[StepCounterFactory] 找到步态检测传感器: {stepDetectorSensor.Name}");
                    Debug.WriteLine($"[StepCounterFactory] - Vendor: {stepDetectorSensor.Vendor}");
                }

                if (stepCounterSensor != null)
                {
                    Debug.WriteLine($"[StepCounterFactory] 找到计步传感器(备份): {stepCounterSensor.Name}");
                }

                if (stepDetectorSensor != null || stepCounterSensor != null)
                {
                    var counter = new HardwareStepCounter();
                    counter.Init(context);
                    Debug.WriteLine("[StepCounterFactory] 硬件步态检测传感器初始化成功");
                    return counter;
                }
            }
            else
            {
                Debug.WriteLine("[StepCounterFactory] 跳过硬件传感器检查，强制使用加速度传感器");
            }

            Debug.WriteLine("[StepCounterFactory] 未找到硬件计步传感器，尝试加速度传感器");

            // 回退到加速度传感器
            var accelSensor = sensorManager.GetDefaultSensor(SensorType.Accelerometer);
            if (accelSensor != null)
            {
                Debug.WriteLine($"[StepCounterFactory] 找到加速度传感器: {accelSensor.Name}");
                Debug.WriteLine($"[StepCounterFactory] - Vendor: {accelSensor.Vendor}");

                var counter = new AccelerometerStepCounter();
                counter.Init(context);
                Debug.WriteLine("[StepCounterFactory] 加速度传感器初始化成功");
                return counter;
            }

            Debug.WriteLine("[StepCounterFactory] 设备不支持任何计步功能，退化为仅节拍器模式");
            return null;
        }
    }
}
