using CommunityToolkit.Mvvm.ComponentModel;

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