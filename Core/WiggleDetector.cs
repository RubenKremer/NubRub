namespace NubRub.Core;

public class WiggleDetector
{
    private System.Timers.Timer? _wiggleTimer;
    private System.Timers.Timer? _pauseTimer;
    private bool _isWiggling;
    private int _wiggleDurationMs;
    private const int PAUSE_THRESHOLD_MS = 2000; // 2 seconds

    public event EventHandler? WiggleDetected;

    public WiggleDetector(int wiggleDurationMs = 25000)
    {
        _wiggleDurationMs = wiggleDurationMs;
        _wiggleTimer = new System.Timers.Timer(_wiggleDurationMs);
        _wiggleTimer.Elapsed += (s, e) => OnWiggleDetected();
        _wiggleTimer.AutoReset = false;

        _pauseTimer = new System.Timers.Timer(PAUSE_THRESHOLD_MS);
        _pauseTimer.Elapsed += (s, e) => OnPauseDetected();
        _pauseTimer.AutoReset = false;
    }

    public int WiggleDurationMs
    {
        get => _wiggleDurationMs;
        set
        {
            _wiggleDurationMs = value;
            if (_wiggleTimer != null)
            {
                _wiggleTimer.Interval = _wiggleDurationMs;
            }
        }
    }

    public void OnMovement()
    {
        if (!_isWiggling)
        {
            // Start wiggling
            _isWiggling = true;
            _wiggleTimer?.Start();
        }

        // Reset pause timer on any movement
        _pauseTimer?.Stop();
        _pauseTimer?.Start();
    }

    private void OnPauseDetected()
    {
        // Movement stopped for >2 seconds, reset wiggle timer
        _isWiggling = false;
        _wiggleTimer?.Stop();
    }

    private void OnWiggleDetected()
    {
        // Continuous wiggling detected after configured duration
        _isWiggling = false;
        _wiggleTimer?.Stop();
        _pauseTimer?.Stop();
        WiggleDetected?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        _isWiggling = false;
        _wiggleTimer?.Stop();
        _pauseTimer?.Stop();
    }

    public void Dispose()
    {
        _wiggleTimer?.Dispose();
        _pauseTimer?.Dispose();
    }
}

