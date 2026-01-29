// 资源设计器，用于访问Android资源
using _Microsoft.Android.Resource.Designer;
// Android媒体相关类，用于音频播放
using Android.Media;
// Android操作系统相关类，包括Handler和Runnable
using Android.OS;
// Java语言相关类，用于实现Runnable接口
using Java.Lang;
// Android应用上下文
using Application = Android.App.Application;
// 系统调试类
using Debug = System.Diagnostics.Debug;
// 数学运算类
using Math = System.Math;

namespace Runner.Client.Platforms.Android.Native
{
    /// <summary>
    /// 步频提示音播放器
    /// 使用SoundPool播放步频节拍音，与音乐等其他音频混合播放
    /// </summary>
    public class CadencePlay : IDisposable
    {
        /// <summary>
        /// 音频池，用于高效播放短音频片段
        /// </summary>
        private SoundPool? _soundPool;
        
        /// <summary>
        /// 提示音的ID
        /// </summary>
        private int _soundId = -1;
        
        /// <summary>
        /// 音频是否已加载完成
        /// </summary>
        private volatile bool _isLoaded;
        
        /// <summary>
        /// 最大并发播放流数量
        /// </summary>
        private const int MAX_STREAMS = 1;
        
        /// <summary>
        /// 线程锁，用于保证线程安全
        /// </summary>
        private readonly Lock _lockObject = new();
        
        /// <summary>
        /// Android Handler，用于在主线程中调度定时任务
        /// </summary>
        private Handler? _handler;
        
        /// <summary>
        /// 定时执行的Runnable任务
        /// </summary>
        private Runnable? _tickRunnable;
        
        /// <summary>
        /// 下一次节拍播放的时间戳
        /// </summary>
        private long _nextTickTime;
        
        /// <summary>
        /// 是否正在运行
        /// </summary>
        private bool _running;
        
        /// <summary>
        /// 节拍间隔时间（毫秒）
        /// </summary>
        private int _intervalMs;

        /// <summary>
        /// 初始化音频播放器
        /// 创建SoundPool并加载提示音资源
        /// </summary>
        public void Init()
        {
            var context = Application.Context;

            // 配置音频属性
            // 使用Game类型以便与音乐等其他音频混合播放
            // 设置低延迟标志以获得更快的响应
            var attr = new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.Game)!
                .SetContentType(AudioContentType.Sonification)!
                .SetFlags(AudioFlags.LowLatency)!
                .Build();

            // 创建SoundPool实例
            _soundPool = new SoundPool.Builder()
                .SetAudioAttributes(attr)!
                .SetMaxStreams(MAX_STREAMS)!
                .Build();

            // 检查SoundPool是否创建成功
            if (_soundPool == null)
            {
                Debug.WriteLine("Failed to create SoundPool");
                return;
            }

            // 注册音频加载完成回调
            _soundPool.LoadComplete += OnLoadComplete;

            // 加载提示音资源
            _soundId = _soundPool.Load(context, ResourceConstant.Raw.beep3, 1);

            Debug.WriteLine($"Loading sound with ID: {_soundId}");

            Debug.WriteLine("SoundPool initialized");
        }

        /// <summary>
        /// 开始播放步频提示音
        /// </summary>
        /// <param name="spm">每分钟步数</param>
        public void Start(int spm)
        {
            // 验证输入参数
            if (spm <= 0) return;
            
            // 先停止之前的播放
            Stop();
            
            // 计算节拍间隔时间（毫秒）
            _intervalMs = (int)(60000.0 / spm);
            
            // 设置下一个节拍的播放时间
            _nextTickTime = SystemClock.UptimeMillis();

            // 设置运行标志
            _running = true;

            // 初始化Handler，用于在主线程中执行定时任务
            if (Looper.MainLooper != null) _handler ??= new Handler(Looper.MainLooper);

            // 创建定时执行的Runnable任务
            _tickRunnable = new Runnable(() =>
            {
                // 检查是否仍在运行
                if (!_running) return;

                // 播放提示音
                PlayTick();

                // 更新下一个节拍的时间
                _nextTickTime += _intervalMs;

                // 计算下次执行的延迟时间
                var delay = Math.Max(0, _nextTickTime - SystemClock.UptimeMillis());

                // 调度下一次执行
                _handler?.PostDelayed(_tickRunnable!, delay);

            });

            // 启动定时任务
            _handler?.Post(_tickRunnable);

            Debug.WriteLine($"Started cadence play at {spm} SPM");
        }

        /// <summary>
        /// 停止播放步频提示音
        /// </summary>
        public void Stop()
        {
            // 设置运行标志为false
            _running = false;
            
            // 从Handler的消息队列中移除定时任务
            _handler?.RemoveCallbacks(_tickRunnable!);
        }

        /// <summary>
        /// 播放单个节拍音
        /// </summary>
        private void PlayTick()
        {
            // 获取当前的_soundPool实例以避免并发问题
            var pool = _soundPool;
            
            // 检查音频是否已加载完成以及相关资源是否有效
            if (_isLoaded && pool != null && _soundId != -1)
            {
                // 播放提示音
                // 参数：音频ID，左声道音量，右声道音量，优先级，循环次数，播放速率
                pool.Play(_soundId, 1f, 1f, 1, 0, 1f);
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // 先停止播放
            Stop();
            
            // 使用锁确保线程安全
            lock (_lockObject)
            {
                // 释放SoundPool资源
                if (_soundPool != null)
                {
                    // 移除音频加载完成回调
                    _soundPool.LoadComplete -= OnLoadComplete;
                    
                    // 释放SoundPool
                    _soundPool.Release();
                    
                    // 将引用设为null
                    _soundPool = null;
                }
                
                // 重置加载标志
                _isLoaded = false;
            }

        }

        /// <summary>
        /// 音频加载完成回调
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">加载完成事件参数</param>
        private void OnLoadComplete(object? sender, SoundPool.LoadCompleteEventArgs e)
        {
            // 检查加载的音频ID是否是我们期望的ID，并且加载状态是否成功
            if (e.SampleId == _soundId && e.Status == 0)
            {
                // 设置音频加载完成标志
                _isLoaded = true;
                
                // 输出调试信息
                Debug.WriteLine("Sound loaded successfully");
            }
        }
    }
}
