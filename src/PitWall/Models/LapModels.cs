namespace PitWall.Models;

public sealed record LapSamplePoint(
    double LapDistPct,
    double SpeedKph,
    double Throttle,
    double Brake,
    double Rpm,
    int Gear,
    double SteeringAngleDeg);

public sealed record LapListItem(
    int LapId,
    string TrackName,
    string CarName,
    int LapNumber,
    double LapTimeSeconds,
    DateTime RecordedAtUtc);
