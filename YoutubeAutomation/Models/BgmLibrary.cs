using System.IO;

namespace YoutubeAutomation.Models;

public static class BgmLibrary
{
    public static readonly string BgmFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YoutubeAutomation", "bgm");

    public static readonly BgmTrack[] Tracks =
    [
        // เดิม 8 tracks
        new("curious-discover.mp3",      "curious",     "สนุกรู้ ค้นพบ",         "2:24"),
        new("curious-wonder.mp3",        "curious",     "สนุกรู้ น่าพิศวง",      "2:33"),
        new("upbeat-fun.mp3",            "upbeat",      "สนุกสนาน",             "2:22"),
        new("upbeat-lively.mp3",         "upbeat",      "สนุกสนาน คึกคัก",      "2:27"),
        new("gentle-nature.mp3",         "gentle",      "อ่อนโยน ธรรมชาติ",     "3:57"),
        new("gentle-soothing.mp3",       "gentle",      "อ่อนโยน สงบ",         "3:45"),
        new("emotional-heartfelt.mp3",   "emotional",   "ซึ้ง อบอุ่น",          "2:01"),
        new("emotional-warm.mp3",        "emotional",   "ซึ้ง ประทับใจ",        "1:52"),

        // ใหม่ 8 tracks (4 moods × 2)
        new("mysterious-suspense.mp3",   "mysterious",  "ลึกลับ ระทึก",         "2:14"),
        new("mysterious-wonder.mp3",     "mysterious",  "ลึกลับ น่าพิศวง",      "1:53"),
        new("dramatic-tension.mp3",      "dramatic",    "ตื่นเต้น ลุ้นระทึก",     "1:37"),
        new("dramatic-cinematic.mp3",    "dramatic",    "ดราม่า ภาพยนตร์",      "2:40"),
        new("epic-grandeur.mp3",         "epic",        "ยิ่งใหญ่ อลังการ",      "3:50"),
        new("epic-cosmic.mp3",           "epic",        "จักรวาล กว้างใหญ่",     "3:46"),
        new("playful-bounce.mp3",        "playful",     "สนุก ร่าเริง",          "2:28"),
        new("playful-quirky.mp3",        "playful",     "แปลกๆ ขำขัน",         "2:22"),
    ];

    public static string? GetTrackPath(string mood)
    {
        var track = Tracks.FirstOrDefault(t => t.Mood == mood)
                 ?? Tracks[0];
        var path = Path.Combine(BgmFolder, track.FileName);
        return File.Exists(path) ? path : null;
    }

    public static bool IsBuiltInTrack(string? filePath)
        => !string.IsNullOrWhiteSpace(filePath)
        && filePath.StartsWith(BgmFolder, StringComparison.OrdinalIgnoreCase);

    public static BgmTrack? FindByPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;
        var fileName = Path.GetFileName(filePath);
        return Tracks.FirstOrDefault(t =>
            t.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
    }

    public static string GetFullPath(BgmTrack track)
        => Path.Combine(BgmFolder, track.FileName);

    public static bool HasAnyTracks()
        => Directory.Exists(BgmFolder)
        && Tracks.Any(t => File.Exists(Path.Combine(BgmFolder, t.FileName)));
}

public record BgmTrack(string FileName, string Mood, string DisplayName, string DurationHint);
