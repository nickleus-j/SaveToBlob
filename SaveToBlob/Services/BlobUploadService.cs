using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace SaveToBlob.Services;

public interface IBlobUploadService
{
    /// <summary>Verifies the connection string and ensures the container exists.</summary>
    Task<BlobContainerClient> GetContainerClientAsync(
        BlobStorageSettings settings, CancellationToken ct = default);

    Task UploadAsync(
        BlobContainerClient container,
        string blobName,
        Stream content,
        string contentType = "application/zip",
        CancellationToken ct = default);
}

public class BlobUploadService : IBlobUploadService
{
    public async Task<BlobContainerClient> GetContainerClientAsync(
        BlobStorageSettings settings, CancellationToken ct = default)
    {
        var connectionString = settings.UseAzurite && string.IsNullOrWhiteSpace(settings.ConnectionString)
            ? BlobStorageSettingsService.AzuriteConnectionString
            : settings.ConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("No blob storage connection string configured.");

        var serviceClient = new BlobServiceClient(connectionString);
        var containerClient = serviceClient.GetBlobContainerClient(settings.ContainerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: ct);
        return containerClient;
    }

    public async Task UploadAsync(
        BlobContainerClient container,
        string blobName,
        Stream content,
        string contentType = "application/zip",
        CancellationToken ct = default)
    {
        var blobClient = container.GetBlobClient(blobName);

        var headers = new BlobHttpHeaders { ContentType = contentType };

        // Retry loop gives resilience on flaky emulator/network connections.
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                content.Position = 0;
                await blobClient.UploadAsync(content, new BlobUploadOptions
                {
                    HttpHeaders = headers,
                    Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
                }, ct);
                return;
            }
            catch (RequestFailedException) when (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt), ct);
            }
        }
    }
}
