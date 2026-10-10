using AVFoundation;
using Foundation;
using Runner.Business.Media;

namespace Runner.Business.Platforms.iOS.Media;

/// <summary>播放真实提示音与拍间静音组成的循环，在锁屏时由 AVAudioPlayer 维持节拍。</summary>
public sealed class IosCadencePlayer(string soundAssetName) : ICadencePlayer
{
    private PcmWaveLoop? _wave;
    private AVAudioPlayer? _player;
    private NSData? _audioData;
    private NSObject? _interruptionObserver;
    private bool _audioSessionActive;
    private int _requestedCadence;
    private bool _disposed;

    public event Action<string>? OnError;

    public async Task InitializeAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_wave != null) return;
        using var source = await FileSystem.OpenAppPackageFileAsync(soundAssetName);
        using var memory = new MemoryStream();
        await source.CopyToAsync(memory);
        _wave = PcmWaveLoop.Read(memory.ToArray());
        _interruptionObserver ??= AVAudioSession.Notifications.ObserveInterruption(OnInterruption);
    }

    public void Start(int stepsPerMinute)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (stepsPerMinute <= 0) return;
        if (_wave == null) throw new InvalidOperationException("节拍音尚未加载");
        StopPlayer();
        try
        {
            ActivateAudio();

            _audioData = NSData.FromArray(_wave.Build(stepsPerMinute));
            _player = AVAudioPlayer.FromData(_audioData, out var playerError)
                ?? throw new InvalidOperationException(playerError?.LocalizedDescription ?? "创建节拍播放器失败");
            _player.NumberOfLoops = -1;
            _player.Volume = 1;
            _player.PrepareToPlay();
            if (!_player.Play()) throw new InvalidOperationException("播放节拍音失败");
            _requestedCadence = stepsPerMinute;
        }
        catch
        {
            Stop();
            throw;
        }
    }

    public void Stop()
    {
        _requestedCadence = 0;
        StopPlayer();
        if (_audioSessionActive)
            AVAudioSession.SharedInstance().SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation, out _);
        _audioSessionActive = false;
    }

    private void ActivateAudio()
    {
        var session = AVAudioSession.SharedInstance();
        var categoryError = session.SetCategory(AVAudioSessionCategory.Playback, AVAudioSessionCategoryOptions.MixWithOthers);
        if (categoryError != null) throw new InvalidOperationException(categoryError.LocalizedDescription);
        if (!session.SetActive(true, out var activeError))
            throw new InvalidOperationException(activeError?.LocalizedDescription ?? "激活音频会话失败");
        _audioSessionActive = true;
    }

    private void OnInterruption(object? sender, AVAudioSessionInterruptionEventArgs e)
    {
        var kind = e.InterruptionType;
        var options = kind == AVAudioSessionInterruptionType.Ended ? e.Option : 0;
        MainThread.BeginInvokeOnMainThread(() => HandleInterruption(kind, options));
    }

    private void HandleInterruption(AVAudioSessionInterruptionType kind, AVAudioSessionInterruptionOptions options)
    {
        if (_disposed || _requestedCadence == 0) return;
        if (kind == AVAudioSessionInterruptionType.Began)
        {
            _audioSessionActive = false;
            return;
        }
        if (!options.HasFlag(AVAudioSessionInterruptionOptions.ShouldResume))
        {
            OnError?.Invoke("节拍音被系统中断，可暂停后继续恢复播放");
            return;
        }
        try
        {
            ActivateAudio();
            if (_player?.Play() != true) throw new InvalidOperationException("恢复节拍音失败");
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"恢复节拍音失败: {ex.Message}");
        }
    }

    private void StopPlayer()
    {
        _player?.Stop();
        _player?.Dispose();
        _player = null;
        _audioData?.Dispose();
        _audioData = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
        _wave = null;
        _interruptionObserver?.Dispose();
        _interruptionObserver = null;
        OnError = null;
    }
}
