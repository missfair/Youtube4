namespace YoutubeAutomation.Models;

/// <summary>
/// Represents a topic suggested by AI with its associated category
/// </summary>
public class SuggestedTopic
{
    /// <summary>
    /// The topic text (e.g., "ทำไมช้างกลัวหนู")
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// The category used to generate this topic
    /// </summary>
    public ContentCategory Category { get; set; }

    public SuggestedTopic(string topic, ContentCategory category)
    {
        Topic = topic;
        Category = category;
    }
}
