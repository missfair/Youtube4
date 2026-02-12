using YoutubeAutomation.Models;

namespace YoutubeAutomation.Services.Interfaces;

public interface IOpenRouterService
{
    Task<string> GenerateTextAsync(
        string prompt,
        string model,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);

    Task<string> GenerateImagePromptAsync(
        string topic,
        string model,
        CancellationToken cancellationToken = default,
        ContentCategory? category = null);

    Task<byte[]> GenerateImageAsync(
        string prompt,
        string model,
        string? referenceImagePath = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default,
        string? topicTitle = null,
        ContentCategory? category = null);

    Task<byte[]> GenerateSceneImageFromCloudAsync(
        string prompt,
        string model,
        string? referenceImagePath = null,
        string aspectRatio = "16:9",
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);

    Task<bool> TestConnectionAsync(string apiKey);
}
