using RepPortal.Services;
using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace ChapSureShipHistoryLoader;

internal sealed class HistoryLoader(
    LoaderOptions options,
    IReadOnlyDictionary<string, string> connectionStrings,
    IChapSureShipHistoryApiClient? apiClient)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var checkpoint = options.Commit
            ? await LoaderCheckpoint.LoadAsync(options.CheckpointPath, cancellationToken)
            : new LoaderCheckpoint();

        Console.WriteLine(options.Commit
            ? "COMMIT MODE: records will be inserted through the MT ION API."
            : "DRY RUN: source data will be read, but no ION writes will be made.");
        Console.WriteLine($"Sites: {string.Join(", ", options.Sites)}; stage: {options.Stage}; batch: {options.BatchSize}");

        var allStats = new List<StageStats>();
        var entireRun = Stopwatch.StartNew();
        foreach (var site in options.Sites)
        {
            var siteTimer = Stopwatch.StartNew();
            var source = new SureShipSourceReader(connectionStrings[site], site);
            if (options.Stage is LoaderStage.All or LoaderStage.Headers)
                allStats.Add(await LoadHeadersAsync(source, checkpoint, site, cancellationToken));
            if (options.Stage is LoaderStage.All or LoaderStage.Orders)
                allStats.Add(await LoadOrdersAsync(source, checkpoint, site, cancellationToken));
            siteTimer.Stop();
            PrintSiteSummary(site, allStats.Where(stat => stat.Site == site), siteTimer.Elapsed);
        }
        entireRun.Stop();
        Console.WriteLine($"All selected sites completed in {FormatDuration(entireRun.Elapsed)}.");
    }

    private async Task<StageStats> LoadHeadersAsync(SureShipSourceReader source, LoaderCheckpoint checkpoint,
        string site, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var checkpointStage = $"headers:{FilterScope()}";
        var cursor = checkpoint.Get(site, checkpointStage);
        var processed = 0;
        var insertedTotal = 0;
        var checkCalls = 0;
        var insertCalls = 0;
        var batches = 0;
        while (!LimitReached(processed))
        {
            var take = Take(processed);
            var rows = await ReadSourceWithRetryAsync(
                () => source.ReadHeadersAsync(cursor, take, options.StartDate,
                    options.EndDateExclusive, cancellationToken), site, "headers", cancellationToken);
            if (rows.Count == 0) break;

            var inserted = 0;
            if (options.Commit)
            {
                checkCalls++;
                var existing = await apiClient!.GetExistingHeaderKeysAsync(site,
                    rows[0].ShipmentId, rows[^1].ShipmentId, cancellationToken);
                var missing = rows.Where(row => !existing.Contains(row.ShipmentId)).ToArray();
                if (missing.Length > 0) insertCalls++;
                await apiClient.InsertHeadersAsync(site, missing, cancellationToken);
                inserted = missing.Length;
            }

            cursor = new Cursor(rows[^1].ShipmentId);
            processed += rows.Count;
            insertedTotal += inserted;
            batches++;
            await PersistIfCommitted(checkpoint, site, checkpointStage, cursor, cancellationToken);
            ReportProgress(site, "headers", batches, processed, insertedTotal, cursor);
        }
        timer.Stop();
        return new StageStats(site, "headers", processed, insertedTotal,
            options.Commit ? processed - insertedTotal : 0, batches, checkCalls, insertCalls, timer.Elapsed);
    }

    private async Task<StageStats> LoadOrdersAsync(SureShipSourceReader source, LoaderCheckpoint checkpoint,
        string site, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var checkpointStage = $"orders:{FilterScope()}";
        var cursor = checkpoint.Get(site, checkpointStage);
        var processed = 0;
        var insertedTotal = 0;
        var checkCalls = 0;
        var insertCalls = 0;
        var batches = 0;
        while (!LimitReached(processed))
        {
            var take = Take(processed);
            var rows = await ReadSourceWithRetryAsync(
                () => source.ReadOrdersAsync(cursor, take, options.StartDate,
                    options.EndDateExclusive, cancellationToken), site, "orders", cancellationToken);
            if (rows.Count == 0) break;

            var inserted = 0;
            if (options.Commit)
            {
                checkCalls++;
                var existing = await apiClient!.GetExistingOrderKeysAsync(site,
                    rows[0].ShipmentId, rows[^1].ShipmentId, cancellationToken);
                var missing = rows.Where(row => !existing.Contains((row.ShipmentId, row.OrderNum))).ToArray();
                if (missing.Length > 0) insertCalls++;
                await apiClient.InsertOrdersAsync(site, missing, cancellationToken);
                inserted = missing.Length;
            }

            cursor = new Cursor(rows[^1].ShipmentId, rows[^1].OrderNum);
            processed += rows.Count;
            insertedTotal += inserted;
            batches++;
            await PersistIfCommitted(checkpoint, site, checkpointStage, cursor, cancellationToken);
            ReportProgress(site, "orders", batches, processed, insertedTotal, cursor);
        }
        timer.Stop();
        return new StageStats(site, "orders", processed, insertedTotal,
            options.Commit ? processed - insertedTotal : 0, batches, checkCalls, insertCalls, timer.Elapsed);
    }

    private bool LimitReached(int processed) => options.MaxRows.HasValue && processed >= options.MaxRows;
    private int Take(int processed) => Math.Min(options.BatchSize,
        options.MaxRows.HasValue ? options.MaxRows.Value - processed : options.BatchSize);

    private string FilterScope() =>
        $"{options.StartDate:yyyyMMdd}-{options.EndDateExclusive:yyyyMMdd}";

    private async Task PersistIfCommitted(LoaderCheckpoint checkpoint, string site, string stage,
        Cursor cursor, CancellationToken cancellationToken)
    {
        if (!options.Commit) return;
        checkpoint.Set(site, stage, cursor);
        await checkpoint.SaveAsync(options.CheckpointPath, cancellationToken);
    }

    private static async Task<List<T>> ReadSourceWithRetryAsync<T>(Func<Task<List<T>>> read,
        string site, string stage, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await read();
            }
            catch (SqlException exception) when (attempt <= 3)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                Console.WriteLine($"{site} {stage}: source SQL attempt {attempt} failed " +
                                  $"({exception.Number}); retrying in {delay.TotalSeconds:N0}s.");
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static void ReportProgress(string site, string stage, int batches, int processed,
        int inserted, Cursor cursor)
    {
        if (batches == 1 || batches % 25 == 0)
            Console.WriteLine($"{site} {stage}: batches {batches:N0}, source {processed:N0}, inserted {inserted:N0}; cursor {cursor.ShipmentId}/{cursor.OrderNum}");
    }

    private static void PrintSiteSummary(string site, IEnumerable<StageStats> stats, TimeSpan elapsed)
    {
        Console.WriteLine($"{site} summary ({FormatDuration(elapsed)}):");
        foreach (var stat in stats)
        {
            var rate = stat.Elapsed.TotalSeconds > 0 ? stat.Inserted / stat.Elapsed.TotalSeconds : 0;
            Console.WriteLine($"  {stat.Stage}: source {stat.SourceRows:N0}, inserted {stat.Inserted:N0}, " +
                              $"existing {stat.Existing:N0}, batches {stat.Batches:N0}, " +
                              $"ION calls {stat.TotalIonCalls:N0} ({stat.CheckCalls:N0} checks + {stat.InsertCalls:N0} inserts), " +
                              $"elapsed {FormatDuration(stat.Elapsed)}, {rate:N1} inserted/sec");
        }
        Console.WriteLine($"  total ION calls: {stats.Sum(stat => stat.TotalIonCalls):N0}");
    }

    private static string FormatDuration(TimeSpan value) =>
        $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
}

internal sealed record StageStats(string Site, string Stage, int SourceRows, int Inserted,
    int Existing, int Batches, int CheckCalls, int InsertCalls, TimeSpan Elapsed)
{
    public int TotalIonCalls => CheckCalls + InsertCalls;
}
