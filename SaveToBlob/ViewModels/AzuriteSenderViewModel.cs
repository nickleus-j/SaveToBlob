using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveToBlob.Services;
using Microsoft.Maui.Storage;
namespace SaveToBlob.ViewModels;

public partial class AzuriteSenderViewModel : ObservableObject
{
    private readonly IZipService _zipService;
    private readonly IBlobUploadService _uploadService;
    private readonly IBlobStorageSettingsService _settingsService;

    public AzuriteSenderViewModel(IZipService zipService, IBlobUploadService uploadService, IBlobStorageSettingsService settingsService)
    {
        _zipService = zipService;
        _uploadService = uploadService;
        _settingsService = settingsService;

        var storageSettings = settingsService.Load();
        _connectionString = storageSettings.ConnectionString;
        _containerName = storageSettings.ContainerName;
        _useAzurite = storageSettings.UseAzurite;
        _useCustomConnection = !storageSettings.UseAzurite;
    }

    public ObservableCollection<PickedFile> Files { get; } = new();

    // ---- Storage mode (mutually exclusive radio buttons) -------------------

    [ObservableProperty] private bool _useAzurite;
    [ObservableProperty] private bool _useCustomConnection;
    [ObservableProperty] private string _connectionString = string.Empty;
    [ObservableProperty] private string _containerName,zipNamePrefix;

    // Keep the two options exclusive and mirror UseAzurite into the persisted
    // settings model (UseCustomConnection is just its inverse).
    partial void OnUseAzuriteChanged(bool value)
    {
        if (value && UseCustomConnection)
            _useCustomConnection = false;
        OnPropertyChanged(nameof(UseCustomConnection));
    }

    partial void OnUseCustomConnectionChanged(bool value)
    {
        if (value && UseAzurite)
            _useAzurite = false;
        OnPropertyChanged(nameof(UseAzurite));
    }

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "Pick files, then compress & upload.";

    // The FileResult handles survive only for the session — we keep them so
    // streams can be re-read later in the pipeline.
    private readonly List<FileResult> _pickedResults = new();

    [RelayCommand]
    private async Task PickFilesAsync()
    {
        try
        {
            var results = await FilePicker.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Select files to zip and upload"
                // Omit FileTypes to allow all files; add e.g.
                // FileTypes = FilePickerFileType.Images to restrict.
            });

            if (results is null || results.Count() == 0) return;

            foreach (FileResult fr in results)
            {
                _pickedResults.Add(fr);
                Files.Add(new PickedFile
                {
                    FileName = fr.FileName,
                    SizeBytes = await GetLengthAsync(fr)
                });
            }

            StatusMessage = $"{Files.Count} file(s) queued.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"File picking failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearFiles()
    {
        Files.Clear();
        _pickedResults.Clear();
        StatusMessage = "Selection cleared.";
    }

    [RelayCommand]
    private void RemoveFile(PickedFile file)
    {
        var index = Files.IndexOf(file);
        if (index >= 0)
        {
            Files.RemoveAt(index);
            _pickedResults.RemoveAt(index);
        }
    }

    [RelayCommand]
    private async Task CompressAndUploadAsync(CancellationToken token)
    {
        if (_pickedResults.Count == 0)
        {
            StatusMessage = "Nothing to upload — pick some files first.";
            return;
        }

        // Validate the chosen storage mode up-front for a clear error.
        if (!UseAzurite && string.IsNullOrWhiteSpace(ConnectionString))
        {
            StatusMessage = "✗ Enter a connection string or choose the Azurite option.";
            return;
        }

        IsBusy = true;
        try
        {
            var settings = new BlobStorageSettings(ConnectionString, ContainerName, UseAzurite);
            _settingsService.Save(settings);

            StatusMessage = "Compressing files…";
            var zipName = _zipService.BuildArchiveName();

            var (zipData, entryNames) = await _zipService.CreateZipAsync(
                _pickedResults.Select(r => (r.FileName, (Func<Task<Stream>>)(() => r.OpenReadAsync()))),
                token);

            StatusMessage = $"Zip built ({zipData.Length / 1024.0:F0} KB, {entryNames.Count} entries). Uploading…";

            var container = await _uploadService.GetContainerClientAsync(settings, token);

            using var zipStream = new MemoryStream(zipData);
            await _uploadService.UploadAsync(container, zipName, zipStream, contentType: "application/zip", ct: token);

            StatusMessage = $"✓ Uploaded '{zipName}' to container '{ContainerName}'.";
            ClearFiles();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Upload cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"✗ Upload failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static async Task<long> GetLengthAsync(FileResult file)
    {
        try
        {
            await using var s = await file.OpenReadAsync();
            return s.Length;
        }
        catch
        {
            return -1;
        }
    }
}
