namespace PitWall.Models;

public sealed record TelemetrySnapshot(
    double SpeedKph,
    double Rpm,
    int Gear,
    double Throttle,
    double Brake,
    double Clutch,
    double SteeringAngleDeg,
    double SteeringTorqueNm,
    double SteeringPctTorque,
    int Lap,
    double LapDistPct,
    double LapCurrentTime,
    double LapLastTime,
    double LapBestTime,
    double FuelLevelPct,
    double LatAccelG,
    double LongAccelG,
    bool IsOnTrack,
    double SessionTime);

public sealed record SessionSummary(string TrackName, string CarName);
