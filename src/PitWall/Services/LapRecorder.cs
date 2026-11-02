using PitWall.Models;

namespace PitWall.Services;

/// <summary>
/// Bridges live telemetry snapshots into completed-lap records, throttling how densely
/// samples are stored so a lap stays a few hundred rows instead of ~1200 (60Hz * ~20s).
/// </summary>
public sealed class LapRecorder
{
    private const double MinLapDistPctStep = 0.001;

    private readonly LapDatabaseService _database;

    private int? _sessionId;
    private string? _sessionTrackName;
    private string? _sessionCarName;

    private int? _currentLapNumber;
    private double _lastRecordedLapDistPct = -1;
    private readonly List<LapSamplePoint> _buffer = [];

    public event Action<LapListItem>? LapSaved;

    public LapRecorder(LapDatabaseService database)
    {
        _database = database;
    }

    public void OnSessionInfo(SessionSummary session)
    {
        if (session.TrackName == _sessionTrackName && session.CarName == _sessionCarName) return;

        _sessionTrackName = session.TrackName;
        _sessionCarName = session.CarName;
        _sessionId = _database.CreateSession(session.TrackName, session.CarName);

        // Track/car changed mid-run (or first session) — any in-progress lap belongs to
        // the old context and can no longer be attributed correctly.
        _currentLapNumber = null;
        _buffer.Clear();
        _lastRecordedLapDistPct = -1;
    }

    public void OnSnapshot(TelemetrySnapshot snapshot)
    {
        if (_sessionId == null) return;
        if (!snapshot.IsOnTrack) return;

        if (_currentLapNumber == null)
        {
            _currentLapNumber = snapshot.Lap;
            _buffer.Clear();
            _lastRecordedLapDistPct = -1;
        }
        else if (snapshot.Lap != _currentLapNumber)
        {
            FlushCompletedLap(snapshot.LapLastTime);
            _currentLapNumber = snapshot.Lap;
        }

        if (_lastRecordedLapDistPct >= 0 &&
            Math.Abs(snapshot.LapDistPct - _lastRecordedLapDistPct) < MinLapDistPctStep)
        {
            return;
        }

        _lastRecordedLapDistPct = snapshot.LapDistPct;
        _buffer.Add(new LapSamplePoint(
            snapshot.LapDistPct,
            snapshot.SpeedKph,
            snapshot.Throttle,
            snapshot.Brake,
            snapshot.Rpm,
            snapshot.Gear,
            snapshot.SteeringAngleDeg));
    }

    private void FlushCompletedLap(double lapTimeSeconds)
    {
        if (_sessionId == null || _currentLapNumber == null) return;
        if (_buffer.Count < 10 || lapTimeSeconds <= 0)
        {
            _buffer.Clear();
            _lastRecordedLapDistPct = -1;
            return;
        }

        var lapId = _database.SaveLap(_sessionId.Value, _currentLapNumber.Value, lapTimeSeconds, _buffer);

        LapSaved?.Invoke(new LapListItem(
            lapId, _sessionTrackName!, _sessionCarName!, _currentLapNumber.Value, lapTimeSeconds, DateTime.UtcNow));

        _buffer.Clear();
        _lastRecordedLapDistPct = -1;
    }
}
