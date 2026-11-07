namespace SaveToBlob.Services;
public interface IZipService
{
    /// <summary>Picks a safe archive name derived from the current timestamp.</summary>
    string BuildArchiveName();

    /// <summary>Creates a zip archive containing all provided files.</summary>
    Task<(byte[] ZipData, IReadOnlyList<string> EntryNames)> CreateZipAsync(
        IEnumerable<(string FileName, Func<Task<Stream>> OpenStream)> files,
        CancellationToken cancellationToken = default);
}

public class ZipService : IZipService
{
    public string BuildArchiveName() =>
        $"upload_{DateTime.Now:yyyyMMdd_HHmmss}.zip";

    public async Task<(byte[] ZipData, IReadOnlyList<string> EntryNames)> CreateZipAsync(
        IEnumerable<(string FileName, Func<Task<Stream>> OpenStream)> files,
        CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        var entryNames = new List<string>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var archive = new System.IO.Compression.ZipArchive(
                   memoryStream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (fileName, openStream) in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // De-duplicate entry names (multiple pickers/sources may collide).
                var entryName = fileName;
                int i = 1;
                while (!usedNames.Add(entryName))
                    entryName = $"{Path.GetFileNameWithoutExtension(fileName)}_{i++}{Path.GetExtension(fileName)}";

                var entry = archive.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Optimal);

                await using (var entryStream = entry.Open())
                await using (var sourceStream = await openStream())
                {
                    await sourceStream.CopyToAsync(entryStream, cancellationToken);
                }

                entryNames.Add(entryName);
            }
        }

        memoryStream.Position = 0;
        return (memoryStream.ToArray(), entryNames);
    }
}
