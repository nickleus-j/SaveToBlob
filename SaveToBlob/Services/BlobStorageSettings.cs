namespace SaveToBlob.Services;

public record BlobStorageSettings(
    string ConnectionString,
    string ContainerName,
    bool UseAzurite);

public interface IBlobStorageSettingsService
{
    BlobStorageSettings Load();
    void Save(BlobStorageSettings settings);
}

public class BlobStorageSettingsService : IBlobStorageSettingsService
{
    private const string KeyConnectionString = "blob.connectionString";
    private const string KeyContainer = "blob.container";
    private const string KeyUseAzurite = "blob.useAzurite";

    // Well-known Azurite development credentials.
    public const string AzuriteConnectionString =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/" +
        "K1SZFPTOtr/KBHBeksoGMGw==;" +
        "BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

    public BlobStorageSettings Load() => new(
        ConnectionString: Preferences.Get(KeyConnectionString, string.Empty),
        ContainerName: Preferences.Get(KeyContainer, "uploads"),
        UseAzurite: Preferences.Get(KeyUseAzurite, false));

    public void Save(BlobStorageSettings settings)
    {
        Preferences.Set(KeyConnectionString, settings.ConnectionString);
        Preferences.Set(KeyContainer, settings.ContainerName);
        Preferences.Set(KeyUseAzurite, settings.UseAzurite);
    }
}