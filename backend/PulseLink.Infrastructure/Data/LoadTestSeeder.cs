using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PulseLink.Core.Entities;
using PulseLink.Core.Enums;

namespace PulseLink.Infrastructure.Data;

public sealed class LoadTestSeedOptions
{
    public int IncidentCount { get; init; } = 70_000;
    public int SpreadDays { get; init; } = 180;
    public int MinDistinctDays { get; init; } = 100;
    public int Seed { get; init; } = 42;

    public static LoadTestSeedOptions Full { get; } = new();

    public static LoadTestSeedOptions ForTests { get; } = new()
    {
        IncidentCount = 800,
        SpreadDays = 90,
        MinDistinctDays = 40,
        Seed = 42
    };
}

public sealed class LoadTestSeedResult
{
    public int Incidents { get; init; }
    public int Vitals { get; init; }
    public int Interventions { get; init; }
    public int AuditEvents { get; init; }
    public DateTime MinUpdatedAtUtc { get; init; }
    public DateTime MaxUpdatedAtUtc { get; init; }
    public int DistinctDays { get; init; }
    public int DefaultTimestampCount { get; init; }
}

public static class LoadTestIds
{
    public const string NumberPrefix = "LT-";
    public const string UserPrefix = "loadtest-user-";

    public static readonly Guid MetroAgencyId = Guid.Parse("aa000001-0000-4000-8000-000000000001");
    public static readonly Guid RegionalAgencyId = Guid.Parse("aa000001-0000-4000-8000-000000000002");
    public static readonly Guid RuralAgencyId = Guid.Parse("aa000001-0000-4000-8000-000000000008");
    public static readonly Guid SparseAgencyId = Guid.Parse("aa000001-0000-4000-8000-00000000000a");

    public static readonly Guid MetroHospitalId = Guid.Parse("bb000001-0000-4000-8000-000000000001");
    public static readonly Guid SparseHospitalId = Guid.Parse("bb000001-0000-4000-8000-000000000008");

    public const string MetroParamedicUserId = UserPrefix + "metro-0";
}

/// <summary>
/// Opt-in large, skewed incident set for index/plan measurement. Not used by the demo seed.
/// Inserts bypass SaveChanges (bulk copy / batched SQL), so UpdatedAtUtc is set on every row.
/// </summary>
public static class LoadTestSeeder
{
    private static readonly string[] Complaints =
    [
        "Chest pain", "Shortness of breath", "Fall", "Syncope", "Abdominal pain",
        "Altered mental status", "Seizure", "Traffic collision", "Allergic reaction", "Weakness"
    ];

    public static async Task<LoadTestSeedResult> SeedAsync(
        PulseLinkDbContext db,
        LoadTestSeedOptions options,
        CancellationToken cancellationToken = default)
    {
        if (await db.Agencies.AnyAsync(a => a.Id == LoadTestIds.MetroAgencyId, cancellationToken))
        {
            var existing = await db.Incidents.CountAsync(
                i => i.IncidentNumber.StartsWith(LoadTestIds.NumberPrefix),
                cancellationToken);
            if (existing >= options.IncidentCount)
            {
                return await MeasureAsync(db, options, cancellationToken);
            }

            await DeleteIncompleteLoadTestDataAsync(db, cancellationToken);
        }

        var agencies = CreateAgencies();
        var hospitals = CreateHospitals();
        db.Agencies.AddRange(agencies);
        db.Hospitals.AddRange(hospitals);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        var rng = new Random(options.Seed);
        var rows = BuildIncidents(agencies, hospitals, options, rng);
        var vitals = new List<VitalSign>(rows.Count / 5);
        var interventions = new List<Intervention>(rows.Count / 8);
        var audits = new List<AuditEvent>(rows.Count);

        foreach (var incident in rows)
        {
            audits.Add(new AuditEvent
            {
                Id = Guid.NewGuid(),
                IncidentId = incident.Id,
                ActorUserId = incident.CreatedByUserId,
                Action = "IncidentCreated",
                Details = "Load-test seed",
                CreatedAt = incident.CreatedAt
            });

            if (rng.NextDouble() < 0.15)
            {
                var n = rng.Next(1, 4);
                for (var i = 0; i < n; i++)
                {
                    vitals.Add(new VitalSign
                    {
                        Id = Guid.NewGuid(),
                        IncidentId = incident.Id,
                        RecordedAt = incident.CreatedAt.AddMinutes(i * 5),
                        HeartRate = 60 + rng.Next(0, 50),
                        SystolicBp = 110 + rng.Next(0, 40),
                        DiastolicBp = 70 + rng.Next(0, 20),
                        RespiratoryRate = 12 + rng.Next(0, 10),
                        SpO2 = 94 + rng.Next(0, 6),
                        TemperatureC = 36.0m + rng.Next(0, 20) / 10m
                    });
                }
            }

            if (rng.NextDouble() < 0.10)
            {
                interventions.Add(new Intervention
                {
                    Id = Guid.NewGuid(),
                    IncidentId = incident.Id,
                    PerformedAt = incident.CreatedAt.AddMinutes(8),
                    Name = rng.NextDouble() < 0.5 ? "Oxygen" : "Aspirin",
                    Route = "PO"
                });
            }
        }

        await InsertIncidentsAsync(db, rows, cancellationToken);
        await InsertVitalsAsync(db, vitals, cancellationToken);
        await InsertInterventionsAsync(db, interventions, cancellationToken);
        await InsertAuditsAsync(db, audits, cancellationToken);

        return await MeasureAsync(db, options, cancellationToken);
    }

    public static async Task<LoadTestSeedResult> MeasureAsync(
        PulseLinkDbContext db,
        LoadTestSeedOptions options,
        CancellationToken cancellationToken = default)
    {
        var loadTest = db.Incidents.Where(i => i.IncidentNumber.StartsWith(LoadTestIds.NumberPrefix));
        var incidents = await loadTest.CountAsync(cancellationToken);
        if (incidents == 0)
        {
            throw new InvalidOperationException("Load-test incidents were not found after seed.");
        }

        var min = await loadTest.MinAsync(i => i.UpdatedAtUtc, cancellationToken);
        var max = await loadTest.MaxAsync(i => i.UpdatedAtUtc, cancellationToken);
        var defaultCount = await loadTest.CountAsync(
            i => i.UpdatedAtUtc <= new DateTime(2000, 1, 1),
            cancellationToken);
        var distinctDays = await CountDistinctDaysAsync(db, cancellationToken);
        var vitals = await db.VitalSigns.CountAsync(
            v => db.Incidents.Any(i => i.Id == v.IncidentId && i.IncidentNumber.StartsWith(LoadTestIds.NumberPrefix)),
            cancellationToken);
        var interventions = await db.Interventions.CountAsync(
            v => db.Incidents.Any(i => i.Id == v.IncidentId && i.IncidentNumber.StartsWith(LoadTestIds.NumberPrefix)),
            cancellationToken);
        var audits = await db.AuditEvents.CountAsync(
            v => v.Details == "Load-test seed",
            cancellationToken);

        var result = new LoadTestSeedResult
        {
            Incidents = incidents,
            Vitals = vitals,
            Interventions = interventions,
            AuditEvents = audits,
            MinUpdatedAtUtc = min,
            MaxUpdatedAtUtc = max,
            DistinctDays = distinctDays,
            DefaultTimestampCount = defaultCount
        };

        if (defaultCount != 0)
        {
            throw new InvalidOperationException(
                $"Load-test seed left {defaultCount} incidents with default UpdatedAtUtc. Bulk insert must set the column explicitly.");
        }

        if (distinctDays < options.MinDistinctDays)
        {
            throw new InvalidOperationException(
                $"Load-test UpdatedAtUtc spans {distinctDays} distinct days; expected at least {options.MinDistinctDays} so plans are not a single-day burst.");
        }

        return result;
    }

    private static async Task<int> CountDistinctDaysAsync(PulseLinkDbContext db, CancellationToken cancellationToken)
    {
        if (db.Database.IsSqlServer())
        {
            return await db.Database.SqlQueryRaw<int>(
                    """
                    SELECT COUNT(DISTINCT CAST([UpdatedAtUtc] AS date)) AS [Value]
                    FROM [Incidents]
                    WHERE [IncidentNumber] LIKE 'LT-%'
                    """)
                .SingleAsync(cancellationToken);
        }

        return await db.Database.SqlQueryRaw<int>(
                """
                SELECT COUNT(DISTINCT date(UpdatedAtUtc)) AS Value
                FROM Incidents
                WHERE IncidentNumber LIKE 'LT-%'
                """)
            .SingleAsync(cancellationToken);
    }

    private static async Task DeleteIncompleteLoadTestDataAsync(
        PulseLinkDbContext db,
        CancellationToken cancellationToken)
    {
        if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                DELETE FROM VitalSigns WHERE IncidentId IN (SELECT Id FROM Incidents WHERE IncidentNumber LIKE 'LT-%');
                DELETE FROM Interventions WHERE IncidentId IN (SELECT Id FROM Incidents WHERE IncidentNumber LIKE 'LT-%');
                DELETE FROM AuditEvents WHERE Details = 'Load-test seed';
                DELETE FROM Incidents WHERE IncidentNumber LIKE 'LT-%';
                DELETE FROM Agencies WHERE Name LIKE 'LoadTest %';
                DELETE FROM Hospitals WHERE Name LIKE 'LoadTest %';
                """,
                cancellationToken: cancellationToken);
            return;
        }

        await db.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM VitalSigns WHERE IncidentId IN (SELECT Id FROM Incidents WHERE IncidentNumber LIKE 'LT-%');
            DELETE FROM Interventions WHERE IncidentId IN (SELECT Id FROM Incidents WHERE IncidentNumber LIKE 'LT-%');
            DELETE FROM AuditEvents WHERE Details = 'Load-test seed';
            DELETE FROM Incidents WHERE IncidentNumber LIKE 'LT-%';
            DELETE FROM Agencies WHERE Name LIKE 'LoadTest %';
            DELETE FROM Hospitals WHERE Name LIKE 'LoadTest %';
            """,
            cancellationToken: cancellationToken);
    }

    private static List<Agency> CreateAgencies() =>
    [
        new() { Id = LoadTestIds.MetroAgencyId, Name = "LoadTest Metro EMS", Region = "Load-Metro" },
        new() { Id = LoadTestIds.RegionalAgencyId, Name = "LoadTest Regional EMS", Region = "Load-Regional" },
        new() { Id = Guid.Parse("aa000001-0000-4000-8000-000000000003"), Name = "LoadTest County EMS", Region = "Load-County" },
        new() { Id = Guid.Parse("aa000001-0000-4000-8000-000000000004"), Name = "LoadTest City EMS", Region = "Load-City" },
        new() { Id = Guid.Parse("aa000001-0000-4000-8000-000000000005"), Name = "LoadTest North EMS", Region = "Load-North" },
        new() { Id = Guid.Parse("aa000001-0000-4000-8000-000000000006"), Name = "LoadTest East EMS", Region = "Load-East" },
        new() { Id = Guid.Parse("aa000001-0000-4000-8000-000000000007"), Name = "LoadTest West EMS", Region = "Load-West" },
        new() { Id = LoadTestIds.RuralAgencyId, Name = "LoadTest Rural EMS", Region = "Load-Rural" },
        new() { Id = Guid.Parse("aa000001-0000-4000-8000-000000000009"), Name = "LoadTest Sparse A EMS", Region = "Load-Sparse" },
        new() { Id = LoadTestIds.SparseAgencyId, Name = "LoadTest Sparse B EMS", Region = "Load-Sparse" }
    ];

    private static List<Hospital> CreateHospitals() =>
    [
        new() { Id = LoadTestIds.MetroHospitalId, Name = "LoadTest Metro General", City = "Metro" },
        new() { Id = Guid.Parse("bb000001-0000-4000-8000-000000000002"), Name = "LoadTest Regional Medical", City = "Regional" },
        new() { Id = Guid.Parse("bb000001-0000-4000-8000-000000000003"), Name = "LoadTest County Hospital", City = "County" },
        new() { Id = Guid.Parse("bb000001-0000-4000-8000-000000000004"), Name = "LoadTest City Hospital", City = "City" },
        new() { Id = Guid.Parse("bb000001-0000-4000-8000-000000000005"), Name = "LoadTest North Hospital", City = "North" },
        new() { Id = Guid.Parse("bb000001-0000-4000-8000-000000000006"), Name = "LoadTest East Hospital", City = "East" },
        new() { Id = Guid.Parse("bb000001-0000-4000-8000-000000000007"), Name = "LoadTest West Hospital", City = "West" },
        new() { Id = LoadTestIds.SparseHospitalId, Name = "LoadTest Sparse Hospital", City = "Sparse" }
    ];

    private static List<Incident> BuildIncidents(
        IReadOnlyList<Agency> agencies,
        IReadOnlyList<Hospital> hospitals,
        LoadTestSeedOptions options,
        Random rng)
    {
        var weights = new (Guid AgencyId, int Count, int Users)[]
        {
            (LoadTestIds.MetroAgencyId, Share(options.IncidentCount, 0.54), 8),
            (LoadTestIds.RegionalAgencyId, Share(options.IncidentCount, 0.25), 4),
            (agencies[2].Id, Share(options.IncidentCount, 0.10), 3),
            (agencies[3].Id, Share(options.IncidentCount, 0.055), 2),
            (agencies[4].Id, Share(options.IncidentCount, 0.022), 2),
            (agencies[5].Id, Share(options.IncidentCount, 0.012), 1),
            (agencies[6].Id, Share(options.IncidentCount, 0.004), 1),
            (LoadTestIds.RuralAgencyId, Math.Max(20, Share(options.IncidentCount, 0.0015)), 1),
            (agencies[8].Id, Math.Max(8, Share(options.IncidentCount, 0.0003)), 1),
            (LoadTestIds.SparseAgencyId, Math.Max(5, Share(options.IncidentCount, 0.00015)), 1)
        };

        var allocated = weights.Sum(w => w.Count);
        var remainder = options.IncidentCount - allocated;
        weights[0] = (weights[0].AgencyId, weights[0].Count + remainder, weights[0].Users);

        var hospitalWeights = new[] { 40, 22, 12, 8, 5, 3, 2, 1 };
        var hospitalThresholds = Cumulative(hospitalWeights);
        var statusRoll = new[]
        {
            IncidentStatus.Draft, IncidentStatus.Draft, IncidentStatus.Draft,
            IncidentStatus.EnRoute, IncidentStatus.OnScene,
            IncidentStatus.Transporting, IncidentStatus.Transporting,
            IncidentStatus.Arrived, IncidentStatus.Arrived,
            IncidentStatus.HandedOff
        };

        var now = DateTimeOffset.UtcNow;
        var rows = new List<Incident>(options.IncidentCount);
        var seq = 1;

        foreach (var (agencyId, count, users) in weights)
        {
            for (var i = 0; i < count; i++)
            {
                var created = SpreadTimestamp(now, options.SpreadDays, rng);
                var updated = created.AddMinutes(rng.Next(0, 180));
                var status = statusRoll[rng.Next(statusRoll.Length)];
                Guid? hospitalId = null;
                if (status is IncidentStatus.Transporting or IncidentStatus.Arrived or IncidentStatus.HandedOff
                    || rng.NextDouble() > 0.12)
                {
                    hospitalId = hospitals[PickWeighted(rng, hospitalThresholds)].Id;
                }

                if (status is IncidentStatus.Transporting or IncidentStatus.Arrived or IncidentStatus.HandedOff)
                {
                    hospitalId ??= LoadTestIds.MetroHospitalId;
                }

                var userId = LoadTestIds.UserPrefix + AgencyUserKey(agencyId) + "-" + (i % users);
                rows.Add(new Incident
                {
                    Id = Guid.NewGuid(),
                    IncidentNumber = $"{LoadTestIds.NumberPrefix}{seq:D8}",
                    Status = status,
                    AgencyId = agencyId,
                    DestinationHospitalId = hospitalId,
                    CreatedByUserId = userId,
                    ChiefComplaint = Complaints[rng.Next(Complaints.Length)],
                    PatientAgeRange = "40-49",
                    PatientSex = rng.NextDouble() < 0.5 ? "M" : "F",
                    CreatedAt = created,
                    UpdatedAt = updated,
                    UpdatedAtUtc = updated.UtcDateTime,
                    HandedOffAt = status == IncidentStatus.HandedOff ? updated : null
                });
                seq++;
            }
        }

        var orExtras = Math.Max(40, options.IncidentCount / 200);
        for (var i = 0; i < orExtras; i++)
        {
            var created = SpreadTimestamp(now, options.SpreadDays, rng);
            var updated = created.AddMinutes(rng.Next(0, 120));
            rows.Add(new Incident
            {
                Id = Guid.NewGuid(),
                IncidentNumber = $"{LoadTestIds.NumberPrefix}{seq:D8}",
                Status = IncidentStatus.Draft,
                AgencyId = LoadTestIds.RuralAgencyId,
                DestinationHospitalId = LoadTestIds.SparseHospitalId,
                CreatedByUserId = LoadTestIds.MetroParamedicUserId,
                ChiefComplaint = "Cross-agency created-by row",
                CreatedAt = created,
                UpdatedAt = updated,
                UpdatedAtUtc = updated.UtcDateTime
            });
            seq++;
        }

        return rows;
    }

    private static DateTimeOffset SpreadTimestamp(DateTimeOffset now, int spreadDays, Random rng)
    {
        var roll = rng.NextDouble();
        var daysBack = roll switch
        {
            < 0.70 => rng.NextDouble() * (spreadDays * 0.5),
            < 0.90 => (spreadDays * 0.5) + rng.NextDouble() * (spreadDays * 0.3),
            _ => (spreadDays * 0.8) + rng.NextDouble() * (spreadDays * 0.2)
        };
        var minutes = rng.Next(0, 24 * 60);
        return now.AddDays(-daysBack).AddMinutes(-minutes);
    }

    private static int Share(int total, double fraction) => (int)Math.Round(total * fraction);

    private static int[] Cumulative(int[] weights)
    {
        var c = new int[weights.Length];
        var sum = 0;
        for (var i = 0; i < weights.Length; i++)
        {
            sum += weights[i];
            c[i] = sum;
        }

        return c;
    }

    private static int PickWeighted(Random rng, int[] cumulative)
    {
        var roll = rng.Next(cumulative[^1]);
        for (var i = 0; i < cumulative.Length; i++)
        {
            if (roll < cumulative[i])
            {
                return i;
            }
        }

        return cumulative.Length - 1;
    }

    private static string AgencyUserKey(Guid agencyId)
    {
        if (agencyId == LoadTestIds.MetroAgencyId) return "metro";
        if (agencyId == LoadTestIds.RegionalAgencyId) return "regional";
        if (agencyId == LoadTestIds.RuralAgencyId) return "rural";
        if (agencyId == LoadTestIds.SparseAgencyId) return "sparseb";
        return agencyId.ToString("N")[..6];
    }

    private static async Task InsertIncidentsAsync(
        PulseLinkDbContext db,
        List<Incident> rows,
        CancellationToken cancellationToken)
    {
        if (db.Database.IsSqlServer())
        {
            await BulkCopySqlServerAsync(db, "Incidents", BuildIncidentTable(rows), cancellationToken);
            return;
        }

        await InsertSqliteIncidentsAsync(db, rows, cancellationToken);
    }

    private static async Task InsertVitalsAsync(
        PulseLinkDbContext db,
        List<VitalSign> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return;
        }

        if (db.Database.IsSqlServer())
        {
            var table = new DataTable();
            table.Columns.Add("Id", typeof(Guid));
            table.Columns.Add("IncidentId", typeof(Guid));
            table.Columns.Add("RecordedAt", typeof(DateTimeOffset));
            table.Columns.Add("HeartRate", typeof(int));
            table.Columns.Add("SystolicBp", typeof(int));
            table.Columns.Add("DiastolicBp", typeof(int));
            table.Columns.Add("RespiratoryRate", typeof(int));
            table.Columns.Add("SpO2", typeof(decimal));
            table.Columns.Add("TemperatureC", typeof(decimal));
            table.Columns.Add("GlasgowComaScale", typeof(string));
            foreach (var v in rows)
            {
                table.Rows.Add(
                    v.Id, v.IncidentId, v.RecordedAt,
                    (object?)v.HeartRate ?? DBNull.Value,
                    (object?)v.SystolicBp ?? DBNull.Value,
                    (object?)v.DiastolicBp ?? DBNull.Value,
                    (object?)v.RespiratoryRate ?? DBNull.Value,
                    (object?)v.SpO2 ?? DBNull.Value,
                    (object?)v.TemperatureC ?? DBNull.Value,
                    (object?)v.GlasgowComaScale ?? DBNull.Value);
            }

            await BulkCopySqlServerAsync(db, "VitalSigns", table, cancellationToken);
            return;
        }

        db.ChangeTracker.AutoDetectChangesEnabled = false;
        foreach (var chunk in rows.Chunk(400))
        {
            db.VitalSigns.AddRange(chunk);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }

        db.ChangeTracker.AutoDetectChangesEnabled = true;
    }

    private static async Task InsertInterventionsAsync(
        PulseLinkDbContext db,
        List<Intervention> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return;
        }

        if (db.Database.IsSqlServer())
        {
            var table = new DataTable();
            table.Columns.Add("Id", typeof(Guid));
            table.Columns.Add("IncidentId", typeof(Guid));
            table.Columns.Add("PerformedAt", typeof(DateTimeOffset));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Medication", typeof(string));
            table.Columns.Add("Dose", typeof(string));
            table.Columns.Add("Route", typeof(string));
            table.Columns.Add("Notes", typeof(string));
            foreach (var i in rows)
            {
                table.Rows.Add(
                    i.Id, i.IncidentId, i.PerformedAt, i.Name,
                    (object?)i.Medication ?? DBNull.Value,
                    (object?)i.Dose ?? DBNull.Value,
                    (object?)i.Route ?? DBNull.Value,
                    (object?)i.Notes ?? DBNull.Value);
            }

            await BulkCopySqlServerAsync(db, "Interventions", table, cancellationToken);
            return;
        }

        db.ChangeTracker.AutoDetectChangesEnabled = false;
        foreach (var chunk in rows.Chunk(400))
        {
            db.Interventions.AddRange(chunk);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }

        db.ChangeTracker.AutoDetectChangesEnabled = true;
    }

    private static async Task InsertAuditsAsync(
        PulseLinkDbContext db,
        List<AuditEvent> rows,
        CancellationToken cancellationToken)
    {
        if (db.Database.IsSqlServer())
        {
            var table = new DataTable();
            table.Columns.Add("Id", typeof(Guid));
            table.Columns.Add("IncidentId", typeof(Guid));
            table.Columns.Add("ActorUserId", typeof(string));
            table.Columns.Add("Action", typeof(string));
            table.Columns.Add("Details", typeof(string));
            table.Columns.Add("CreatedAt", typeof(DateTimeOffset));
            foreach (var a in rows)
            {
                table.Rows.Add(a.Id, a.IncidentId!, a.ActorUserId, a.Action, a.Details, a.CreatedAt);
            }

            await BulkCopySqlServerAsync(db, "AuditEvents", table, cancellationToken);
            return;
        }

        db.ChangeTracker.AutoDetectChangesEnabled = false;
        foreach (var chunk in rows.Chunk(400))
        {
            db.AuditEvents.AddRange(chunk);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }

        db.ChangeTracker.AutoDetectChangesEnabled = true;
    }

    private static DataTable BuildIncidentTable(List<Incident> rows)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(Guid));
        table.Columns.Add("IncidentNumber", typeof(string));
        table.Columns.Add("Status", typeof(int));
        table.Columns.Add("AgencyId", typeof(Guid));
        table.Columns.Add("DestinationHospitalId", typeof(Guid));
        table.Columns.Add("CreatedByUserId", typeof(string));
        table.Columns.Add("PatientAgeRange", typeof(string));
        table.Columns.Add("PatientSex", typeof(string));
        table.Columns.Add("ChiefComplaint", typeof(string));
        table.Columns.Add("Notes", typeof(string));
        table.Columns.Add("CreatedAt", typeof(DateTimeOffset));
        table.Columns.Add("UpdatedAt", typeof(DateTimeOffset));
        table.Columns.Add("UpdatedAtUtc", typeof(DateTime));
        table.Columns.Add("HandedOffAt", typeof(DateTimeOffset));
        foreach (var i in rows)
        {
            table.Rows.Add(
                i.Id,
                i.IncidentNumber,
                (int)i.Status,
                i.AgencyId,
                (object?)i.DestinationHospitalId ?? DBNull.Value,
                i.CreatedByUserId,
                (object?)i.PatientAgeRange ?? DBNull.Value,
                (object?)i.PatientSex ?? DBNull.Value,
                i.ChiefComplaint,
                (object?)i.Notes ?? DBNull.Value,
                i.CreatedAt,
                i.UpdatedAt,
                i.UpdatedAtUtc,
                (object?)i.HandedOffAt ?? DBNull.Value);
        }

        return table;
    }

    private static async Task BulkCopySqlServerAsync(
        PulseLinkDbContext db,
        string tableName,
        DataTable table,
        CancellationToken cancellationToken)
    {
        var connectionString = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("SQL Server bulk seed requires a connection string.");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        using var copy = new SqlBulkCopy(connection)
        {
            DestinationTableName = tableName,
            BatchSize = 4000,
            BulkCopyTimeout = 600
        };
        foreach (DataColumn column in table.Columns)
        {
            copy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        await copy.WriteToServerAsync(table, cancellationToken);
    }

    private static async Task InsertSqliteIncidentsAsync(
        PulseLinkDbContext db,
        List<Incident> rows,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
        }

        await using var tx = await connection.BeginTransactionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            """
            INSERT INTO Incidents
            (Id, IncidentNumber, Status, AgencyId, DestinationHospitalId, CreatedByUserId,
             PatientAgeRange, PatientSex, ChiefComplaint, Notes, CreatedAt, UpdatedAt, UpdatedAtUtc, HandedOffAt)
            VALUES
            ($Id, $IncidentNumber, $Status, $AgencyId, $DestinationHospitalId, $CreatedByUserId,
             $PatientAgeRange, $PatientSex, $ChiefComplaint, $Notes, $CreatedAt, $UpdatedAt, $UpdatedAtUtc, $HandedOffAt)
            """;
        var pId = cmd.CreateParameter(); pId.ParameterName = "$Id"; cmd.Parameters.Add(pId);
        var pNum = cmd.CreateParameter(); pNum.ParameterName = "$IncidentNumber"; cmd.Parameters.Add(pNum);
        var pStatus = cmd.CreateParameter(); pStatus.ParameterName = "$Status"; cmd.Parameters.Add(pStatus);
        var pAgency = cmd.CreateParameter(); pAgency.ParameterName = "$AgencyId"; cmd.Parameters.Add(pAgency);
        var pHosp = cmd.CreateParameter(); pHosp.ParameterName = "$DestinationHospitalId"; cmd.Parameters.Add(pHosp);
        var pUser = cmd.CreateParameter(); pUser.ParameterName = "$CreatedByUserId"; cmd.Parameters.Add(pUser);
        var pAge = cmd.CreateParameter(); pAge.ParameterName = "$PatientAgeRange"; cmd.Parameters.Add(pAge);
        var pSex = cmd.CreateParameter(); pSex.ParameterName = "$PatientSex"; cmd.Parameters.Add(pSex);
        var pCc = cmd.CreateParameter(); pCc.ParameterName = "$ChiefComplaint"; cmd.Parameters.Add(pCc);
        var pNotes = cmd.CreateParameter(); pNotes.ParameterName = "$Notes"; cmd.Parameters.Add(pNotes);
        var pCreated = cmd.CreateParameter(); pCreated.ParameterName = "$CreatedAt"; cmd.Parameters.Add(pCreated);
        var pUpdated = cmd.CreateParameter(); pUpdated.ParameterName = "$UpdatedAt"; cmd.Parameters.Add(pUpdated);
        var pUtc = cmd.CreateParameter(); pUtc.ParameterName = "$UpdatedAtUtc"; cmd.Parameters.Add(pUtc);
        var pHandoff = cmd.CreateParameter(); pHandoff.ParameterName = "$HandedOffAt"; cmd.Parameters.Add(pHandoff);

        foreach (var i in rows)
        {
            pId.Value = GuidText(i.Id);
            pNum.Value = i.IncidentNumber;
            pStatus.Value = (int)i.Status;
            pAgency.Value = GuidText(i.AgencyId);
            pHosp.Value = i.DestinationHospitalId is null ? DBNull.Value : GuidText(i.DestinationHospitalId.Value);
            pUser.Value = i.CreatedByUserId;
            pAge.Value = (object?)i.PatientAgeRange ?? DBNull.Value;
            pSex.Value = (object?)i.PatientSex ?? DBNull.Value;
            pCc.Value = i.ChiefComplaint;
            pNotes.Value = (object?)i.Notes ?? DBNull.Value;
            pCreated.Value = i.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss.fffffffzzz");
            pUpdated.Value = i.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss.fffffffzzz");
            pUtc.Value = i.UpdatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss.fffffff");
            pHandoff.Value = i.HandedOffAt is null
                ? DBNull.Value
                : i.HandedOffAt.Value.ToString("yyyy-MM-dd HH:mm:ss.fffffffzzz");
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
    }

    private static string GuidText(Guid value) => value.ToString().ToUpperInvariant();
}
