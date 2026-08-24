using System.Globalization;

namespace ChapSureShipHistoryLoader;

internal enum LoaderStage { All, Headers, Orders }

internal sealed record LoaderOptions(
    bool Commit,
    IReadOnlyList<string> Sites,
    LoaderStage Stage,
    int BatchSize,
    int? MaxRows,
    DateTime? StartDate,
    DateTime? EndDateExclusive,
    string CheckpointPath,
    bool SourceIntegratedSecurity)
{
    public static LoaderOptions Parse(string[] args, string repositoryRoot)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var argument = args[i];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Unexpected argument '{argument}'.");
            var equals = argument.IndexOf('=');
            if (equals > 2)
                values[argument[2..equals]] = argument[(equals + 1)..];
            else if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                values[argument[2..]] = args[++i];
            else
                values[argument[2..]] = null;
        }

        var commit = values.ContainsKey("commit");
        var sites = Get(values, "sites", "BAT,KENT").Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(site => site.ToUpperInvariant()).Distinct().ToArray();
        if (sites.Length == 0 || sites.Any(site => site is not ("BAT" or "KENT")))
            throw new ArgumentException("--sites must contain BAT, KENT, or both.");

        if (!Enum.TryParse<LoaderStage>(Get(values, "stage", "All"), true, out var stage))
            throw new ArgumentException("--stage must be all, headers, or orders.");
        var batchSize = ParsePositiveInt(Get(values, "batch-size", "100"), "--batch-size");
        if (batchSize > 500)
            throw new ArgumentException("--batch-size cannot exceed 500.");

        int? maxRows = values.TryGetValue("max-rows", out var maxText)
            ? ParsePositiveInt(maxText, "--max-rows")
            : commit ? null : 100;
        var checkpoint = Path.GetFullPath(Get(values, "checkpoint",
            Path.Combine(repositoryRoot, "artifacts", "chap-sureship-history-loader", "checkpoint.json")));

        return new LoaderOptions(commit, sites, stage, batchSize, maxRows,
            ParseDate(values, "start-date"), ParseDate(values, "end-date-exclusive"), checkpoint,
            values.ContainsKey("source-integrated-security"));
    }

    public static string Usage => """
        Usage: dotnet run --project Tools/ChapSureShipHistoryLoader -- [options]

          --commit                    Write to MT. Without this flag the run is read-only.
          --sites BAT,KENT            Source/target sites (default: both).
          --stage all|headers|orders  Stage to load (default: all).
          --batch-size N              ION rows per request, 1-500 (default: 100).
          --max-rows N                Maximum source rows per site/stage.
          --start-date yyyy-MM-dd     Optional inclusive ship date.
          --end-date-exclusive date   Optional exclusive ship date.
          --checkpoint path           Override the restart checkpoint path.
          --source-integrated-security
                                      Use the current Windows identity for source SQL.
          --help                      Show this help.
        """;

    private static string Get(Dictionary<string, string?> values, string key, string fallback) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static int ParsePositiveInt(string? value, string option) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : throw new ArgumentException($"{option} requires a positive integer.");

    private static DateTime? ParseDate(Dictionary<string, string?> values, string key)
    {
        if (!values.TryGetValue(key, out var value))
            return null;
        return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed)
            ? parsed
            : throw new ArgumentException($"--{key} requires yyyy-MM-dd.");
    }
}
