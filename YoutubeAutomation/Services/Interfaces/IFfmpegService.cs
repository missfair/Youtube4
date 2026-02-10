namespace YoutubeAutomation.Services.Interfaces;

public interface IFfmpegService
{
    Task<string> CreateVideoFromImageAndAudioAsync(
        string imagePath,
        List<string> audioFiles,
        string outputPath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);

    TimeSpan GetAudioDuration(string audioPath);

    Task<bool> TestFfmpegAsync(string ffmpegPath);

    Task<bool> TestNvencAsync();

    Task<string> CreateMultiImageVideoAsync(
        List<(string imagePath, double durationSeconds)> sceneImages,
        List<string> audioFiles,
        string outputPath,
        bool useGpu = false,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
}
