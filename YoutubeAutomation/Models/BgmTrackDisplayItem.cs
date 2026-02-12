using System.IO;

namespace YoutubeAutomation.Models;

public class BgmTrackDisplayItem
{
    public BgmTrack? Track { get; init; }
    public string DisplayText { get; init; } = "";
    public bool IsCustomBrowse { get; init; }
    public string? CustomFilePath { get; init; }

    public static BgmTrackDisplayItem BrowseCustom => new()
    {
        IsCustomBrowse = true,
        DisplayText = "📁 เลือกไฟล์เอง..."
    };

    public static BgmTrackDisplayItem FromTrack(BgmTrack track) => new()
    {
        Track = track,
        DisplayText = $"{track.DisplayName}  ({track.DurationHint})"
    };

    public static BgmTrackDisplayItem FromCustomFile(string filePath) => new()
    {
        DisplayText = $"📄 {Path.GetFileName(filePath)}",
        CustomFilePath = filePath
    };

    public override string ToString() => DisplayText;
}
