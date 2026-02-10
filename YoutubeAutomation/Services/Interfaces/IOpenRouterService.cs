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
        CancellationToken cancellationToken = default);

    Task<byte[]> GenerateImageAsync(
        string prompt,
        string model,
        string? referenceImagePath = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default,
        string? topicTitle = null);

    Task<bool> TestConnectionAsync(string apiKey);
}
