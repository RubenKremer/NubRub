using System.Reflection;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NubRub.Models;

namespace NubRub.Services;

public class AudioPlayer : IDisposable
{
    private WaveOutEvent? _waveOut;
    private LoopStream? _loopStream;
    private List<AudioFileReader> _squeakReaders = new();
    private List<AudioFileReader> _triggerReaders = new();
    private System.Timers.Timer? _idleTimer;
    private bool _isPlaying;
    private bool _hasRecentMovement = false;
    private bool _isInitialized = false;
    private bool _isInCooldown = false;
    private bool _isEnabled = true;
    private double _volume = 0.6;
    private int _idleCutoffMs = 250;
    private string _audioPack = "squeak";
    private AudioPackManager? _packManager;
    private Random _random = new();
    private const int STARTUP_DELAY_MS = 1000;
    private const int TRIGGER_COOLDOWN_MS = 5000;

    public event EventHandler? TriggerSoundCompleted;

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0.0, 1.0);
            foreach (var reader in _squeakReaders)
            {
                reader.Volume = (float)_volume;
            }
            foreach (var reader in _triggerReaders)
            {
                reader.Volume = (float)_volume;
            }
        }
    }

    public string AudioPack
    {
        get => _audioPack;
        set
        {
            if (_audioPack != value)
            {
                _audioPack = value;
                LoadAudioFiles();
            }
        }
    }

    public int IdleCutoffMs
    {
        get => _idleCutoffMs;
        set
        {
            _idleCutoffMs = value;
            if (_idleTimer != null)
                _idleTimer.Interval = _idleCutoffMs;
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            if (!_isEnabled)
            {
                // If disabling, stop any current playback
                StopSqueak();
            }
        }
    }

    public AudioPlayer(AudioPackManager? packManager = null)
    {
        _packManager = packManager ?? new AudioPackManager();
        LoadAudioFiles();
        InitializeIdleTimer();
        
        // Mark as initialized after a short delay
        System.Threading.Tasks.Task.Delay(STARTUP_DELAY_MS).ContinueWith(_ =>
        {
            _isInitialized = true;
        });
    }

    private void LoadAudioFiles()
    {
        try
        {
            // Dispose existing readers
            foreach (var reader in _squeakReaders)
            {
                reader?.Dispose();
            }
            _squeakReaders.Clear();
            foreach (var reader in _triggerReaders)
            {
                reader?.Dispose();
            }
            _triggerReaders.Clear();

            var packInfo = _packManager?.GetPack(_audioPack);
            if (packInfo == null)
            {
                // Fallback to built-in pack loading
                LoadBuiltInPack(_audioPack);
                return;
            }

            if (packInfo.IsBuiltIn)
            {
                // Load from embedded resources
                LoadBuiltInPack(_audioPack);
            }
            else
            {
                // Load from file system (custom pack)
                LoadCustomPack(packInfo);
            }
        }
        catch
        {
            // If audio files don't exist, create silent placeholders
            // This allows the app to run without audio files
        }
    }

    private void LoadBuiltInPack(string packId)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            string packPrefix = packId;

            // Load squeak sounds (up to 6) from embedded resources
            // Missing files are skipped gracefully
            for (int i = 1; i <= 6; i++)
            {
                string resourceName = $"NubRub.Resources.sounds.{packPrefix}.{packPrefix}-{i}.wav";
                var squeakResource = assembly.GetManifestResourceStream(resourceName);
                if (squeakResource != null)
                {
                    // Save to temp file since AudioFileReader needs a file path
                    string tempSqueak = Path.Combine(Path.GetTempPath(), $"NubRub_{packPrefix}-{i}.wav");
                    using (var fileStream = File.Create(tempSqueak))
                    {
                        squeakResource.CopyTo(fileStream);
                    }
                    var reader = new AudioFileReader(tempSqueak);
                    reader.Volume = (float)_volume;
                    _squeakReaders.Add(reader);
                }
            }

            // Load trigger sound from embedded resources
            string triggerResourceName = $"NubRub.Resources.sounds.{packPrefix}.{packPrefix}-trigger.wav";
            var triggerResource = assembly.GetManifestResourceStream(triggerResourceName);
            if (triggerResource != null)
            {
                // Save to temp file since AudioFileReader needs a file path
                string tempTrigger = Path.Combine(Path.GetTempPath(), $"NubRub_{packPrefix}-trigger.wav");
                using (var fileStream = File.Create(tempTrigger))
                {
                    triggerResource.CopyTo(fileStream);
                }
                var reader = new AudioFileReader(tempTrigger);
                reader.Volume = (float)_volume;
                _triggerReaders.Add(reader);
            }
        }
        catch
        {
        }
    }

    private void LoadCustomPack(AudioPackInfo packInfo)
    {
        try
        {
            // Load rub sounds (movement sounds)
            foreach (var fileName in packInfo.RubSounds)
            {
                string? filePath = _packManager?.GetAudioFilePath(packInfo, fileName);
                if (filePath != null && File.Exists(filePath))
                {
                    try
                    {
                        var reader = new AudioFileReader(filePath);
                        reader.Volume = (float)_volume;
                        _squeakReaders.Add(reader);
                    }
                    catch
                    {
                        // Skip invalid audio files
                        continue;
                    }
                }
            }

            // Load finish sounds (trigger sounds)
            foreach (var fileName in packInfo.FinishSounds)
            {
                string? filePath = _packManager?.GetAudioFilePath(packInfo, fileName);
                if (filePath != null && File.Exists(filePath))
                {
                    try
                    {
                        var reader = new AudioFileReader(filePath);
                        reader.Volume = (float)_volume;
                        _triggerReaders.Add(reader);
                    }
                    catch
                    {
                        // Skip invalid audio files
                        continue;
                    }
                }
            }
        }
        catch
        {
        }
    }

    private void InitializeIdleTimer()
    {
        _idleTimer = new System.Timers.Timer(_idleCutoffMs);
        _idleTimer.Elapsed += (s, e) => StopSqueak();
        _idleTimer.AutoReset = false;
    }

    public void StartSqueak()
    {
        if (_squeakReaders.Count == 0) return;

        try
        {
            // If not playing, start a random sound
            if (!_isPlaying)
            {
                PlayNextRandomSound();
            }
            
            _hasRecentMovement = true;
            _idleTimer?.Stop();
            _idleTimer?.Start();
        }
        catch
        {
        }
    }
    
    private void PlayNextRandomSound()
    {
        try
        {
            if (_squeakReaders.Count == 0) return;
            
            // Stop any existing playback
            StopSqueak();
            
            // Randomly select one of the squeak sounds
            int randomIndex = _random.Next(_squeakReaders.Count);
            var selectedReader = _squeakReaders[randomIndex];
            selectedReader.Position = 0;
            
            _loopStream = new LoopStream(selectedReader);
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_loopStream);
            
            // Listen for playback completion to start next sound
            _waveOut.PlaybackStopped += (s, e) =>
            {
                // Only continue if there's been recent movement
                if (_hasRecentMovement && _squeakReaders.Count > 0)
                {
                    // Small delay before starting next sound
                    System.Threading.Tasks.Task.Delay(50).ContinueWith(_ =>
                    {
                        if (_hasRecentMovement)
                        {
                            PlayNextRandomSound();
                        }
                        else
                        {
                            _isPlaying = false;
                        }
                    });
                }
                else
                {
                    _isPlaying = false;
                }
            };
            
            _waveOut.Play();
            _isPlaying = true;
        }
        catch
        {
            _isPlaying = false;
        }
    }

    public void StopSqueak()
    {
        if (!_isPlaying) return;

        try
        {
            _hasRecentMovement = false;
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;
            _loopStream = null;
            _isPlaying = false;
            _idleTimer?.Stop();
        }
        catch
        {
        }
    }

    public void OnMovement()
    {
        // Don't play audio if disabled
        if (!_isEnabled)
        {
            return;
        }
        
        // Don't play audio during startup delay
        if (!_isInitialized)
        {
            return;
        }
        
        // Don't play audio during cooldown period after trigger
        if (_isInCooldown)
        {
            return;
        }
        
        // Mark that there's been recent movement
        _hasRecentMovement = true;
        
        // Start playing if not already playing
        if (!_isPlaying)
        {
            StartSqueak();
        }
        
        _idleTimer?.Stop();
        _idleTimer?.Start();
    }

    public void PlayTriggerSound()
    {
        if (_triggerReaders.Count == 0) return;

        try
        {
            // Stop all squeak sounds immediately
            StopSqueak();
            _hasRecentMovement = false;
            
            _isInCooldown = true;

            // Randomly select a trigger sound if multiple are available
            int randomIndex = _random.Next(_triggerReaders.Count);
            var selectedReader = _triggerReaders[randomIndex];
            selectedReader.Position = 0;
            
            var waveOut = new WaveOutEvent();
            waveOut.Init(selectedReader);
            waveOut.PlaybackStopped += (s, e) =>
            {
                waveOut.Dispose();
                
                // Start cooldown timer - no squeak sounds for 5 seconds
                System.Threading.Tasks.Task.Delay(TRIGGER_COOLDOWN_MS).ContinueWith(_ =>
                {
                    _isInCooldown = false;
                });
                
                TriggerSoundCompleted?.Invoke(this, EventArgs.Empty);
            };
            waveOut.Play();
        }
        catch
        {
            _isInCooldown = true;
            System.Threading.Tasks.Task.Delay(TRIGGER_COOLDOWN_MS).ContinueWith(_ =>
            {
                _isInCooldown = false;
            });
            TriggerSoundCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ReleaseFileLocks()
    {
        // Stop any playing audio
        StopSqueak();
        
        // Dispose all file readers to release file locks
        foreach (var reader in _squeakReaders)
        {
            reader?.Dispose();
        }
        _squeakReaders.Clear();
        foreach (var reader in _triggerReaders)
        {
            reader?.Dispose();
        }
        _triggerReaders.Clear();
        
        // Dispose wave output
        _waveOut?.Dispose();
        _waveOut = null;
        _loopStream?.Dispose();
        _loopStream = null;
    }

    public void Dispose()
    {
        StopSqueak();
        _idleTimer?.Dispose();
        foreach (var reader in _squeakReaders)
        {
            reader?.Dispose();
        }
        _squeakReaders.Clear();
        foreach (var reader in _triggerReaders)
        {
            reader?.Dispose();
        }
        _triggerReaders.Clear();
        _waveOut?.Dispose();
        _loopStream?.Dispose();
    }
}

// Helper class to play audio once (not looped)
public class LoopStream : WaveStream
{
    private readonly WaveStream _sourceStream;

    public LoopStream(WaveStream sourceStream)
    {
        _sourceStream = sourceStream;
    }

    public override WaveFormat WaveFormat => _sourceStream.WaveFormat;
    public override long Length => _sourceStream.Length;
    public override long Position
    {
        get => _sourceStream.Position;
        set => _sourceStream.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        // Play once - when we reach the end (bytesRead == 0), the stream will naturally stop
        // and trigger PlaybackStopped event, which will start the next sound
        return _sourceStream.Read(buffer, offset, count);
    }
}

