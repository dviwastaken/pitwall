using IRSDKSharper;
using PitWall.Models;

namespace PitWall.Services;

public sealed class TelemetryService : IDisposable
{
    private readonly IRacingSdk _sdk;

    public event Action? Connected;
    public event Action? Disconnected;
    public event Action<TelemetrySnapshot>? SnapshotReceived;
    public event Action<SessionSummary>? SessionInfoReceived;

    public bool IsConnected => _sdk.IsConnected;

    public TelemetryService()
    {
        _sdk = new IRacingSdk
        {
            // ~20Hz UI updates instead of the raw 60Hz sim rate
            UpdateInterval = 3,
        };

        _sdk.OnConnected += () => Connected?.Invoke();
        _sdk.OnDisconnected += () => Disconnected?.Invoke();
        _sdk.OnSessionInfo += HandleSessionInfo;
        _sdk.OnTelemetryData += HandleTelemetryData;
    }

    public void Start() => _sdk.Start();

    public void Stop() => _sdk.Stop();

    private void HandleSessionInfo()
    {
        var sessionInfo = _sdk.Data.SessionInfo;
        if (sessionInfo?.WeekendInfo == null) return;

        var trackName = sessionInfo.WeekendInfo.TrackDisplayName ?? "Bilinmeyen Pist";
        var carName = "Bilinmeyen Araç";

        var driverCarIdx = sessionInfo.DriverInfo?.DriverCarIdx;
        var driver = sessionInfo.DriverInfo?.Drivers?.Find(d => d.CarIdx == driverCarIdx);
        if (driver?.CarScreenName != null) carName = driver.CarScreenName;

        SessionInfoReceived?.Invoke(new SessionSummary(trackName, carName));
    }

    private void HandleTelemetryData()
    {
        var data = _sdk.Data;

        var snapshot = new TelemetrySnapshot(
            SpeedKph: SafeGetFloat(data, "Speed") * 3.6,
            Rpm: SafeGetFloat(data, "RPM"),
            Gear: SafeGetInt(data, "Gear"),
            Throttle: SafeGetFloat(data, "Throttle"),
            Brake: SafeGetFloat(data, "Brake"),
            Clutch: SafeGetFloat(data, "Clutch"),
            SteeringAngleDeg: SafeGetFloat(data, "SteeringWheelAngle") * (180.0 / Math.PI),
            SteeringTorqueNm: SafeGetFloat(data, "SteeringWheelTorque"),
            SteeringPctTorque: SafeGetFloat(data, "SteeringWheelPctTorque"),
            Lap: SafeGetInt(data, "Lap"),
            LapDistPct: SafeGetFloat(data, "LapDistPct"),
            LapCurrentTime: SafeGetFloat(data, "LapCurrentLapTime"),
            LapLastTime: SafeGetFloat(data, "LapLastLapTime"),
            LapBestTime: SafeGetFloat(data, "LapBestLapTime"),
            FuelLevelPct: SafeGetFloat(data, "FuelLevelPct"),
            LatAccelG: SafeGetFloat(data, "LatAccel") / 9.80665,
            LongAccelG: SafeGetFloat(data, "LongAccel") / 9.80665,
            IsOnTrack: SafeGetBool(data, "IsOnTrack"),
            SessionTime: SafeGetDouble(data, "SessionTime"));

        SnapshotReceived?.Invoke(snapshot);
    }

    // The telemetry variable set can vary slightly by car/session (and across SDK updates),
    // so a single unknown name must not take down the whole polling loop.
    private static float SafeGetFloat(IRacingSdkData data, string name)
    {
        try { return data.GetFloat(name); } catch { return 0f; }
    }

    private static int SafeGetInt(IRacingSdkData data, string name)
    {
        try { return data.GetInt(name); } catch { return 0; }
    }

    private static bool SafeGetBool(IRacingSdkData data, string name)
    {
        try { return data.GetBool(name); } catch { return false; }
    }

    private static double SafeGetDouble(IRacingSdkData data, string name)
    {
        try { return data.GetDouble(name); } catch { return 0; }
    }

    public void Dispose() => _sdk.Stop();
}
