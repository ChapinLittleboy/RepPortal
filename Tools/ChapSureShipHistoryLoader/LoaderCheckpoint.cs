using System.Text.Json;

namespace ChapSureShipHistoryLoader;

internal sealed class LoaderCheckpoint
{
    public Dictionary<string, Cursor> Cursors { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Cursor Get(string site, string stage) =>
        Cursors.TryGetValue($"{site}:{stage}", out var cursor) ? cursor : new Cursor();

    public void Set(string site, string stage, Cursor cursor) => Cursors[$"{site}:{stage}"] = cursor;

    public static async Task<LoaderCheckpoint> LoadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return new LoaderCheckpoint();
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<LoaderCheckpoint>(stream,
                   cancellationToken: cancellationToken)
               ?? new LoaderCheckpoint();
    }

    public async Task SaveAsync(string path, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporary = path + ".tmp";
        await using (var stream = File.Create(temporary))
            await JsonSerializer.SerializeAsync(stream, this,
                new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
        File.Move(temporary, path, true);
    }
}

internal sealed record Cursor(int ShipmentId = 0, string OrderNum = "");
