using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveToBlob.Services;
using Microsoft.Maui.Storage;
namespace SaveToBlob.ViewModels;

public partial class PickedFile : ObservableObject
{
    public string FileName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }

    public string SizeDisplay =>
        SizeBytes switch
        {
            >= 1_048_576 => $"{SizeBytes / 1048576.0:F1} MB",
            >= 1024      => $"{SizeBytes / 1024.0:F1} KB",
            _            => $"{SizeBytes} B"
        };

    [ObservableProperty]
    private string? _thumbnail;
}

public partial class MainViewModel : ObservableObject
{
    private readonly IZipService _zipService;
    private readonly IBlobUploadService _uploadService;
    private readonly IBlobStorageSettingsService _settingsService;

    public MainViewModel(
        IZipService zipService,
        IBlobUploadService uploadService,
        IBlobStorageSettingsService settingsService)
    {
        _zipService = zipService;
        _uploadService = uploadService;
        _settingsService = settingsService;

        var s = settingsService.Load();
        _connectionString = s.ConnectionString;
        _containerName = s.ContainerName;
        _useAzurite = s.UseAzurite;
    }

    public ObservableCollection<PickedFile> Files { get; } = new();

    [ObservableProperty] private string _connectionString;
    [ObservableProperty] private string _containerName;
    [ObservableProperty] private bool _useAzurite;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "Pick files, then compress & upload.";

    // The FileResult handles survive only for the session — we keep the open
    // delegates so streams can be re-read later in the pipeline.
    private readonly List<FileResult> _pickedResults = new();

    [RelayCommand]
    private async Task PickFilesAsync()
    {
        try
        {
            var results =  await FilePicker.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Select files to zip and upload"
                // Omit FileTypes to allow all files; add e.g.
                // FileTypes = FilePickerFileType.Images to restrict.
            });

            if (results is null || results.Count() == 0) return;

            foreach (var r in results)
            {
                _pickedResults.Add(r!);
                Files.Add(new PickedFile
                {
                    FileName = r.FileName,
                    SizeBytes = await GetLengthAsync(r)
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
            await _uploadService.UploadAsync(container, zipName, zipStream,
                contentType: "application/zip", ct: token);

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

