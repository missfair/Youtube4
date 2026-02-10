using System.IO;

namespace YoutubeAutomation.Models;

public class VideoProject
{
    public int EpisodeNumber { get; set; } = 1;
    public string TopicSubject { get; set; } = "";
    public string FolderName { get; set; } = "";

    // Step 1: Topics
    public List<VideoTopic> GeneratedTopics { get; set; } = new();
    public VideoTopic? SelectedTopic { get; set; }

    // Step 2: Scripts (3 parts)
    public List<Script> Scripts { get; set; } = new();

    // Step 3: Cover Image
    public string CoverImagePrompt { get; set; } = "";
    public string CoverImagePath { get; set; } = "";

    // Step 4: Audio (3 parts)
    public List<AudioSegment> AudioSegments { get; set; } = new();

    // Step 5: Video
    public string FinalVideoPath { get; set; } = "";

    // Output folder
    public string OutputFolder { get; set; } = "";

    public string GetOutputFolderPath(string basePath)
    {
        var folderName = string.IsNullOrEmpty(FolderName)
            ? $"EP{EpisodeNumber}"
            : $"EP{EpisodeNumber} {FolderName}";
        return Path.Combine(basePath, folderName);
    }
}

public class VideoTopic
{
    public int Index { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    public override string ToString() => Title;
}

public class Script
{
    public int PartNumber { get; set; }
    public string Content { get; set; } = "";
    public string FilePath { get; set; } = "";

    public string GetFileName() => $"บท{PartNumber}.docx";
}

public class AudioSegment
{
    public int PartNumber { get; set; }
    public string FilePath { get; set; } = "";
    public TimeSpan Duration { get; set; }

    public string GetFileName(int episodeNumber) => $"ep{episodeNumber}_{PartNumber}.wav";
}
