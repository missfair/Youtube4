namespace YoutubeAutomation.Services.Interfaces;

public interface IVideoHistoryService : IDisposable
{
    /// <summary>
    /// Load all topics from video_history.txt
    /// </summary>
    /// <returns>List of unique topic strings (deduped)</returns>
    Task<List<string>> LoadHistoryAsync();

    /// <summary>
    /// Append a new topic to video_history.txt (thread-safe)
    /// </summary>
    Task SaveTopicAsync(string topic);

    /// <summary>
    /// Migrate topics from existing EP folders to history file
    /// </summary>
    /// <param name="youtubePath">Path to Youtube folder (e.g., C:\Users\user\Downloads\Youtube)</param>
    /// <returns>Count of topics migrated</returns>
    Task<int> MigrateFromFoldersAsync(string youtubePath);

    /// <summary>
    /// Get the path to video_history.txt from settings
    /// </summary>
    string GetHistoryFilePath();
}
