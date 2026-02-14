using System.IO;
using System.Text.RegularExpressions;
using YoutubeAutomation.Models;
using YoutubeAutomation.Services.Interfaces;

namespace YoutubeAutomation.Services;

public class VideoHistoryService : IVideoHistoryService
{
    private readonly ProjectSettings _settings;
    private readonly SemaphoreSlim _fileLock = new(1, 1); // Thread-safe file I/O

    public VideoHistoryService(ProjectSettings settings)
    {
        _settings = settings;
    }

    public string GetHistoryFilePath()
    {
        return _settings.VideoHistoryFilePath;
    }

    public async Task<List<string>> LoadHistoryAsync()
    {
        var filePath = GetHistoryFilePath();

        if (!File.Exists(filePath))
        {
            return new List<string>();
        }

        await _fileLock.WaitAsync();
        try
        {
            var lines = await File.ReadAllLinesAsync(filePath);
            // Deduplicate and remove empty lines
            return lines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveTopicAsync(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return;

        var filePath = GetHistoryFilePath();
        var directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await _fileLock.WaitAsync();
        try
        {
            // Read existing topics
            var existing = new List<string>();
            if (File.Exists(filePath))
            {
                existing = (await File.ReadAllLinesAsync(filePath))
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => line.Trim())
                    .ToList();
            }

            // Check if topic already exists (case-insensitive)
            var trimmedTopic = topic.Trim();
            if (!existing.Any(t => t.Equals(trimmedTopic, StringComparison.OrdinalIgnoreCase)))
            {
                existing.Add(trimmedTopic);

                // Periodically deduplicate and sort (every 10th save or if duplicates detected)
                var distinctCount = existing.Distinct(StringComparer.OrdinalIgnoreCase).Count();
                if (existing.Count != distinctCount || existing.Count % 10 == 0)
                {
                    // Rewrite entire file with deduplicated, sorted topics (atomic write)
                    var deduplicated = existing
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(t => t)
                        .ToList();

                    // Atomic write: write to temp file first, then rename
                    var tempFilePath = filePath + ".tmp";
                    await File.WriteAllLinesAsync(tempFilePath, deduplicated);
                    File.Move(tempFilePath, filePath, overwrite: true);
                    AppLogger.Log($"SaveTopic: Deduplicated history ({existing.Count} → {deduplicated.Count} topics)");
                }
                else
                {
                    // Just append
                    await File.AppendAllLinesAsync(filePath, new[] { trimmedTopic });
                }
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<int> MigrateFromFoldersAsync(string youtubePath)
    {
        if (!Directory.Exists(youtubePath))
        {
            throw new DirectoryNotFoundException($"Youtube folder not found: {youtubePath}");
        }

        var topics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var folders = Directory.GetDirectories(youtubePath)
            .Where(dir => Path.GetFileName(dir).StartsWith("EP", StringComparison.OrdinalIgnoreCase))
            .OrderBy(dir => dir);

        foreach (var folder in folders)
        {
            // Strategy 1: Try reading from หัวข้อเรื่อง.txt
            var topicFilePath = Path.Combine(folder, "หัวข้อเรื่อง.txt");
            if (File.Exists(topicFilePath))
            {
                try
                {
                    var lines = await File.ReadAllLinesAsync(topicFilePath);
                    var topicLine = lines.FirstOrDefault(line => line.StartsWith("หัวข้อ:", StringComparison.OrdinalIgnoreCase));
                    if (topicLine != null)
                    {
                        var topic = topicLine.Substring(topicLine.IndexOf(':') + 1).Trim();
                        if (!string.IsNullOrWhiteSpace(topic))
                        {
                            topics.Add(topic);
                            continue;
                        }
                    }
                }
                catch
                {
                    // If file read fails, fall through to folder name parsing
                }
            }

            // Strategy 2: Extract from folder name (e.g., "EP44 ทำไมช้างกลัวหนู" -> "ทำไมช้างกลัวหนู")
            var folderName = Path.GetFileName(folder);
            // Use greedy capture to get full topic including parentheses (backup filtering happens later)
            var match = Regex.Match(folderName, @"^EP\d+\s+(.+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var topic = match.Groups[1].Value.Trim();
                // Ensure it ends with question mark if it looks like a question
                if (!string.IsNullOrWhiteSpace(topic))
                {
                    // Remove trailing underscores and common suffixes
                    topic = topic.TrimEnd('_', ' ').Trim();

                    // Skip if topic contains backup/version keywords
                    if (topic.Contains("backup", StringComparison.OrdinalIgnoreCase) ||
                        topic.Contains(" v2", StringComparison.OrdinalIgnoreCase) ||
                        topic.Contains(" part ", StringComparison.OrdinalIgnoreCase))
                    {
                        AppLogger.Log($"MigrateFolders: Skipping folder with version/backup suffix: {folderName}");
                        continue;
                    }

                    // Only add question mark if topic is reasonably short (< 50 chars) and starts with question word
                    if (topic.Length < 50 && !topic.EndsWith("?") &&
                        (topic.StartsWith("ทำไม", StringComparison.OrdinalIgnoreCase) ||
                         topic.StartsWith("อย่างไร", StringComparison.OrdinalIgnoreCase) ||
                         topic.StartsWith("เมื่อไร", StringComparison.OrdinalIgnoreCase) ||
                         topic.StartsWith("ที่ไหน", StringComparison.OrdinalIgnoreCase)))
                    {
                        topic += "?";
                    }

                    topics.Add(topic);
                }
            }
        }

        // Write all topics to history file
        var filePath = GetHistoryFilePath();
        var directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await _fileLock.WaitAsync();
        try
        {
            await File.WriteAllLinesAsync(filePath, topics.OrderBy(t => t));
        }
        finally
        {
            _fileLock.Release();
        }

        return topics.Count;
    }

    public void Dispose()
    {
        _fileLock?.Dispose();
    }
}
