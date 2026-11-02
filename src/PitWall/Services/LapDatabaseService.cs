using System.IO;
using Microsoft.Data.Sqlite;
using PitWall.Models;

namespace PitWall.Services;

public sealed class LapDatabaseService
{
    private readonly string _connectionString;

    public LapDatabaseService()
    {
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PitWall");
        Directory.CreateDirectory(dataDir);

        var dbPath = Path.Combine(dataDir, "pitwall.db");
        _connectionString = $"Data Source={dbPath}";

        EnsureCreated();
    }

    private void EnsureCreated()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Sessions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TrackName TEXT NOT NULL,
                CarName TEXT NOT NULL,
                StartedAtUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Laps (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionId INTEGER NOT NULL REFERENCES Sessions(Id),
                LapNumber INTEGER NOT NULL,
                LapTimeSeconds REAL NOT NULL,
                RecordedAtUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS LapSamples (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                LapId INTEGER NOT NULL REFERENCES Laps(Id),
                LapDistPct REAL NOT NULL,
                SpeedKph REAL NOT NULL,
                Throttle REAL NOT NULL,
                Brake REAL NOT NULL,
                Rpm REAL NOT NULL,
                Gear INTEGER NOT NULL,
                SteeringAngleDeg REAL NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_LapSamples_LapId ON LapSamples(LapId);
            """;
        command.ExecuteNonQuery();
    }

    public int CreateSession(string trackName, string carName)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Sessions (TrackName, CarName, StartedAtUtc)
            VALUES ($trackName, $carName, $startedAtUtc);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$trackName", trackName);
        command.Parameters.AddWithValue("$carName", carName);
        command.Parameters.AddWithValue("$startedAtUtc", DateTime.UtcNow.ToString("O"));

        return Convert.ToInt32((long)command.ExecuteScalar()!);
    }

    public int SaveLap(int sessionId, int lapNumber, double lapTimeSeconds, IReadOnlyList<LapSamplePoint> samples)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        int lapId;
        using (var lapCommand = connection.CreateCommand())
        {
            lapCommand.Transaction = transaction;
            lapCommand.CommandText = """
                INSERT INTO Laps (SessionId, LapNumber, LapTimeSeconds, RecordedAtUtc)
                VALUES ($sessionId, $lapNumber, $lapTimeSeconds, $recordedAtUtc);
                SELECT last_insert_rowid();
                """;
            lapCommand.Parameters.AddWithValue("$sessionId", sessionId);
            lapCommand.Parameters.AddWithValue("$lapNumber", lapNumber);
            lapCommand.Parameters.AddWithValue("$lapTimeSeconds", lapTimeSeconds);
            lapCommand.Parameters.AddWithValue("$recordedAtUtc", DateTime.UtcNow.ToString("O"));
            lapId = Convert.ToInt32((long)lapCommand.ExecuteScalar()!);
        }

        using (var sampleCommand = connection.CreateCommand())
        {
            sampleCommand.Transaction = transaction;
            sampleCommand.CommandText = """
                INSERT INTO LapSamples (LapId, LapDistPct, SpeedKph, Throttle, Brake, Rpm, Gear, SteeringAngleDeg)
                VALUES ($lapId, $lapDistPct, $speedKph, $throttle, $brake, $rpm, $gear, $steeringAngleDeg);
                """;
            var lapIdParam = sampleCommand.Parameters.Add("$lapId", SqliteType.Integer);
            var lapDistPctParam = sampleCommand.Parameters.Add("$lapDistPct", SqliteType.Real);
            var speedKphParam = sampleCommand.Parameters.Add("$speedKph", SqliteType.Real);
            var throttleParam = sampleCommand.Parameters.Add("$throttle", SqliteType.Real);
            var brakeParam = sampleCommand.Parameters.Add("$brake", SqliteType.Real);
            var rpmParam = sampleCommand.Parameters.Add("$rpm", SqliteType.Real);
            var gearParam = sampleCommand.Parameters.Add("$gear", SqliteType.Integer);
            var steeringAngleDegParam = sampleCommand.Parameters.Add("$steeringAngleDeg", SqliteType.Real);

            foreach (var sample in samples)
            {
                lapIdParam.Value = lapId;
                lapDistPctParam.Value = sample.LapDistPct;
                speedKphParam.Value = sample.SpeedKph;
                throttleParam.Value = sample.Throttle;
                brakeParam.Value = sample.Brake;
                rpmParam.Value = sample.Rpm;
                gearParam.Value = sample.Gear;
                steeringAngleDegParam.Value = sample.SteeringAngleDeg;
                sampleCommand.ExecuteNonQuery();
            }
        }

        transaction.Commit();
        return lapId;
    }

    public List<LapListItem> GetLaps()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT l.Id, s.TrackName, s.CarName, l.LapNumber, l.LapTimeSeconds, l.RecordedAtUtc
            FROM Laps l
            JOIN Sessions s ON s.Id = l.SessionId
            ORDER BY l.RecordedAtUtc DESC;
            """;

        var results = new List<LapListItem>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new LapListItem(
                LapId: reader.GetInt32(0),
                TrackName: reader.GetString(1),
                CarName: reader.GetString(2),
                LapNumber: reader.GetInt32(3),
                LapTimeSeconds: reader.GetDouble(4),
                RecordedAtUtc: DateTime.Parse(reader.GetString(5)).ToUniversalTime()));
        }
        return results;
    }

    public List<LapSamplePoint> GetLapSamples(int lapId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT LapDistPct, SpeedKph, Throttle, Brake, Rpm, Gear, SteeringAngleDeg
            FROM LapSamples
            WHERE LapId = $lapId
            ORDER BY LapDistPct ASC;
            """;
        command.Parameters.AddWithValue("$lapId", lapId);

        var results = new List<LapSamplePoint>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new LapSamplePoint(
                LapDistPct: reader.GetDouble(0),
                SpeedKph: reader.GetDouble(1),
                Throttle: reader.GetDouble(2),
                Brake: reader.GetDouble(3),
                Rpm: reader.GetDouble(4),
                Gear: reader.GetInt32(5),
                SteeringAngleDeg: reader.GetDouble(6)));
        }
        return results;
    }
}
