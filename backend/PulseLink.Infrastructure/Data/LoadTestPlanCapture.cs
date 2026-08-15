using System.Diagnostics;
using System.Text;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PulseLink.Core.Enums;

namespace PulseLink.Infrastructure.Data;

public static class LoadTestPlanCapture
{
    public static async Task<string> RunAsync(PulseLinkDbContext db, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        var stats = await LoadTestSeeder.MeasureAsync(db, LoadTestSeedOptions.Full);
        var hospitalMatchCount = await db.Incidents.CountAsync(i =>
            i.DestinationHospitalId == LoadTestIds.MetroHospitalId
            && (i.Status == IncidentStatus.Transporting
                || i.Status == IncidentStatus.Arrived
                || i.Status == IncidentStatus.HandedOff));
        const int pageSize = 50;
        const int maxPageSize = 100;
        var deepPage = Math.Max(2, hospitalMatchCount / maxPageSize);

        var queries = BuildQueries(db, hospitalMatchCount, deepPage, pageSize, maxPageSize);
        var report = new StringBuilder();
        report.AppendLine($"# Load-test plans ({DateTime.UtcNow:u})");
        report.AppendLine();
        report.AppendLine($"Engine: {db.Database.ProviderName}");
        report.AppendLine($"Incidents={stats.Incidents} UpdatedAtUtc {stats.MinUpdatedAtUtc:u} .. {stats.MaxUpdatedAtUtc:u} distinctDays={stats.DistinctDays} defaults={stats.DefaultTimestampCount}");
        report.AppendLine($"Hospital list matches (metro + transporting/arrived/handedoff)={hospitalMatchCount}; deep page={deepPage} at pageSize=100");
        report.AppendLine();

        if (!db.Database.IsSqlServer())
        {
            foreach (var query in queries)
            {
                report.AppendLine($"## {query.Name}");
                report.AppendLine();
                report.AppendLine("```sql");
                report.AppendLine(query.Sql);
                report.AppendLine("```");
                report.AppendLine();
            }

            var path = Path.Combine(outputDir, "report.md");
            await File.WriteAllTextAsync(path, report.ToString());
            return path;
        }

        var connectionString = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Plan capture requires a SQL Server connection string.");

        await using (var statsConn = new SqlConnection(connectionString))
        {
            await statsConn.OpenAsync();
            await using var statsCmd = statsConn.CreateCommand();
            statsCmd.CommandText = "UPDATE STATISTICS Incidents WITH FULLSCAN; UPDATE STATISTICS VitalSigns WITH FULLSCAN; UPDATE STATISTICS Interventions WITH FULLSCAN; UPDATE STATISTICS AuditEvents WITH FULLSCAN;";
            statsCmd.CommandTimeout = 120;
            await statsCmd.ExecuteNonQueryAsync();
        }

        foreach (var query in queries)
        {
            var captured = await ExecuteWithPlanAsync(connectionString, query.Sql, query.Name, outputDir);
            report.AppendLine($"## {query.Name}");
            report.AppendLine();
            report.AppendLine(query.Purpose);
            report.AppendLine();
            report.AppendLine("```sql");
            report.AppendLine(query.Sql);
            report.AppendLine("```");
            report.AppendLine();
            report.AppendLine($"- elapsed_ms: {captured.ElapsedMs}");
            report.AppendLine($"- rows: {captured.RowCount}");
            report.AppendLine($"- xml_logical_reads: {captured.XmlLogicalReads}");
            report.AppendLine($"- cpu_ms: {captured.CpuMs}");
            report.AppendLine($"- elapsed_stmt_ms: {captured.StmtElapsedMs}");
            report.AppendLine($"- operators: {captured.Operators}");
            report.AppendLine($"- has_sort: {captured.HasSort}");
            report.AppendLine($"- sort_actual_rows: {captured.SortActualRows}");
            report.AppendLine($"- has_scan: {captured.HasScan}");
            report.AppendLine($"- has_seek: {captured.HasSeek}");
            report.AppendLine($"- est_rows: {captured.EstimatedRows}");
            report.AppendLine($"- actual_rows: {captured.ActualRows}");
            report.AppendLine($"- memory_grant_kb: {captured.MemoryGrantKb}");
            if (!string.IsNullOrEmpty(captured.AccessSummary))
            {
                report.AppendLine($"- access: {captured.AccessSummary}");
            }
            report.AppendLine();
        }

        var reportPath = Path.Combine(outputDir, "report.md");
        await File.WriteAllTextAsync(reportPath, report.ToString());
        return reportPath;
    }

    private static List<CapturedQuery> BuildQueries(
        PulseLinkDbContext db,
        int hospitalMatchCount,
        int deepPage,
        int pageSize,
        int maxPageSize)
    {
        var hospitalFilter = db.Incidents.AsNoTracking().Where(i =>
            i.DestinationHospitalId == LoadTestIds.MetroHospitalId
            && (i.Status == IncidentStatus.Transporting
                || i.Status == IncidentStatus.Arrived
                || i.Status == IncidentStatus.HandedOff));

        var paramedicFilter = IncidentRoleQueries.ForParamedic(
            db.Incidents.AsNoTracking(),
            LoadTestIds.MetroAgencyId,
            LoadTestIds.MetroParamedicUserId);

        var hospitalPage = Page(hospitalFilter.Include(i => i.Agency).Include(i => i.DestinationHospital), 1, pageSize);
        var hospitalDeep = Page(hospitalFilter.Include(i => i.Agency).Include(i => i.DestinationHospital), deepPage, maxPageSize);
        var paramedicPage = Page(paramedicFilter.Include(i => i.Agency).Include(i => i.DestinationHospital), 1, pageSize);

        var detailId = db.Incidents
            .AsNoTracking()
            .Where(i => i.IncidentNumber.StartsWith(LoadTestIds.NumberPrefix) && i.VitalSigns.Any())
            .Select(i => i.Id)
            .First();

        var detail = db.Incidents.AsNoTracking()
            .Include(i => i.Agency)
            .Include(i => i.DestinationHospital)
            .Include(i => i.VitalSigns)
            .Include(i => i.Interventions)
            .Include(i => i.AuditEvents)
            .Where(i => i.Id == detailId);

        var vitalsByIncident = db.VitalSigns.AsNoTracking().Where(v => v.IncidentId == detailId);
        var auditsByIncident = db.AuditEvents.AsNoTracking().Where(a => a.IncidentId == detailId);

        return
        [
            new("a-hospital-page", "Hospital staff list page 1 with Agency/Hospital includes.", hospitalPage.ToQueryString()),
            new("a-hospital-count", "Hospital staff CountAsync over the filtered set.", CountSql(hospitalFilter)),
            new("a-hospital-deep", $"Hospital staff deep page {deepPage} at pageSize {maxPageSize} ({hospitalMatchCount} matches).", hospitalDeep.ToQueryString()),
            new("b-paramedic-page", "Paramedic list UNION of agency and created-by, page 1 with includes.", paramedicPage.ToQueryString()),
            new("b-paramedic-count", "Paramedic CountAsync over the UNION filter.", CountSql(paramedicFilter)),
            new("c-detail", "Detail load by Id with vitals/interventions/audits includes.", detail.ToQueryString()),
            new("d-vitals-by-incident", "VitalSigns lookup by IncidentId.", vitalsByIncident.ToQueryString()),
            new("d-audits-by-incident", "AuditEvents lookup by IncidentId.", auditsByIncident.ToQueryString())
        ];
    }

    private static IQueryable<Core.Entities.Incident> Page(
        IQueryable<Core.Entities.Incident> query,
        int page,
        int pageSize) =>
        query
            .OrderByDescending(i => i.UpdatedAtUtc)
            .ThenByDescending(i => i.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

    private static string CountSql(IQueryable<Core.Entities.Incident> query)
    {
        var sql = query.Select(i => i.Id).ToQueryString();
        var select = sql.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase);
        if (select < 0)
        {
            return sql;
        }

        return sql[..select] + "SELECT COUNT(*) FROM (" + sql[select..] + ") AS [count_src]";
    }

    private static async Task<PlanSnapshot> ExecuteWithPlanAsync(
        string connectionString,
        string sql,
        string name,
        string outputDir)
    {
        if (sql.StartsWith("--", StringComparison.Ordinal))
        {
            return new PlanSnapshot();
        }

        await using var connection = new SqlConnection(connectionString);
        var io = new StringBuilder();
        connection.InfoMessage += (_, e) => io.AppendLine(e.Message);
        await connection.OpenAsync();

        await using (var setup = connection.CreateCommand())
        {
            setup.CommandText = "SET STATISTICS IO ON; SET STATISTICS TIME ON; SET STATISTICS XML ON;";
            await setup.ExecuteNonQueryAsync();
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 120;
        var sw = Stopwatch.StartNew();
        var rows = 0;
        string? planXml = null;
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            do
            {
                if (reader.FieldCount == 1 && reader.GetName(0).Contains("XML", StringComparison.OrdinalIgnoreCase))
                {
                    if (await reader.ReadAsync())
                    {
                        planXml = reader.GetString(0);
                    }

                    continue;
                }

                while (await reader.ReadAsync())
                {
                    rows++;
                }
            } while (await reader.NextResultAsync());
        }

        sw.Stop();

        if (planXml is not null)
        {
            await File.WriteAllTextAsync(Path.Combine(outputDir, name + ".sqlplan.xml"), planXml);
        }

        var snapshot = ParsePlan(planXml);
        snapshot.ElapsedMs = sw.ElapsedMilliseconds;
        snapshot.RowCount = rows;
        ParseIo(io.ToString(), snapshot);
        return snapshot;
    }

    private static PlanSnapshot ParsePlan(string? xml)
    {
        var snapshot = new PlanSnapshot();
        if (string.IsNullOrWhiteSpace(xml))
        {
            return snapshot;
        }

        var doc = XDocument.Parse(xml);
        XNamespace ns = doc.Root?.Name.Namespace ?? "";
        var relops = doc.Descendants(ns + "RelOp").ToList();
        snapshot.Operators = string.Join(", ", relops.Select(r => (string?)r.Attribute("PhysicalOp")).Where(v => v is not null).Distinct());
        snapshot.HasSort = relops.Any(r => (string?)r.Attribute("PhysicalOp") == "Sort");
        snapshot.HasScan = relops.Any(r => ((string?)r.Attribute("PhysicalOp"))?.Contains("Scan", StringComparison.Ordinal) == true);
        snapshot.HasSeek = relops.Any(r => ((string?)r.Attribute("PhysicalOp"))?.Contains("Seek", StringComparison.Ordinal) == true);
        snapshot.EstimatedRows = relops.FirstOrDefault()?.Attribute("EstimateRows")?.Value;
        snapshot.MemoryGrantKb = doc.Descendants(ns + "MemoryGrantInfo").FirstOrDefault()?.Attribute("GrantedMemory")?.Value;

        var xmlReads = 0;
        var access = new List<string>();
        foreach (var rel in relops)
        {
            var phys = (string?)rel.Attribute("PhysicalOp") ?? "";
            var rtc = rel.Element(ns + "RunTimeInformation")?.Element(ns + "RunTimeCountersPerThread");
            var actualRows = rtc?.Attribute("ActualRows")?.Value ?? rel.Attribute("ActualRows")?.Value;
            if (int.TryParse(rtc?.Attribute("ActualLogicalReads")?.Value, out var reads))
            {
                xmlReads += reads;
            }

            if (phys == "Sort")
            {
                snapshot.SortActualRows = actualRows;
            }

            var idx = rel.Descendants(ns + "Object").FirstOrDefault()?.Attribute("Index")?.Value
                ?? rel.Descendants(ns + "Object").FirstOrDefault()?.Attribute("Table")?.Value;
            if (phys is "Index Seek" or "Index Scan" or "Clustered Index Seek" or "Clustered Index Scan" or "Sort")
            {
                access.Add($"{phys} {idx} rows={actualRows} reads={rtc?.Attribute("ActualLogicalReads")?.Value ?? "0"}");
            }
        }

        snapshot.XmlLogicalReads = xmlReads;
        snapshot.AccessSummary = string.Join("; ", access);
        snapshot.ActualRows = relops.FirstOrDefault()?.Element(ns + "RunTimeInformation")
            ?.Element(ns + "RunTimeCountersPerThread")?.Attribute("ActualRows")?.Value
            ?? snapshot.ActualRows;
        return snapshot;
    }

    private static void ParseIo(string info, PlanSnapshot snapshot)
    {
        foreach (var line in info.Split('\n', StringSplitOptions.TrimEntries))
        {
            if (line.Contains("logical reads", StringComparison.OrdinalIgnoreCase))
            {
                var token = line.Split("logical reads", StringSplitOptions.None)[0]
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .LastOrDefault();
                if (int.TryParse(token?.TrimEnd(','), out var reads))
                {
                    snapshot.LogicalReads += reads;
                }
            }

            if (line.StartsWith("SQL Server Execution Times", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (line.Contains("CPU time =", StringComparison.OrdinalIgnoreCase))
            {
                var cpu = ExtractMs(line, "CPU time =");
                var elapsed = ExtractMs(line, "elapsed time =");
                if (cpu is not null) snapshot.CpuMs += cpu.Value;
                if (elapsed is not null) snapshot.StmtElapsedMs += elapsed.Value;
            }
        }
    }

    private static int? ExtractMs(string line, string marker)
    {
        var i = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (i < 0)
        {
            return null;
        }

        var rest = line[(i + marker.Length)..];
        var digits = new string(rest.TakeWhile(c => char.IsDigit(c) || c == ' ').ToArray()).Trim();
        return int.TryParse(digits.Split(' ')[0], out var ms) ? ms : null;
    }

    private sealed record CapturedQuery(string Name, string Purpose, string Sql);

    private sealed class PlanSnapshot
    {
        public long ElapsedMs { get; set; }
        public int RowCount { get; set; }
        public int LogicalReads { get; set; }
        public int XmlLogicalReads { get; set; }
        public int CpuMs { get; set; }
        public int StmtElapsedMs { get; set; }
        public string Operators { get; set; } = "";
        public bool HasSort { get; set; }
        public string? SortActualRows { get; set; }
        public bool HasScan { get; set; }
        public bool HasSeek { get; set; }
        public string? EstimatedRows { get; set; }
        public string? ActualRows { get; set; }
        public string? MemoryGrantKb { get; set; }
        public string AccessSummary { get; set; } = "";
    }
}
