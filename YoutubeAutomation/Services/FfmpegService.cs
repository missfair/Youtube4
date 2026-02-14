using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NAudio.Wave;
using YoutubeAutomation.Models;
using YoutubeAutomation.Services.Interfaces;

namespace YoutubeAutomation.Services;

public class FfmpegService : IFfmpegService
{
    private readonly ProjectSettings _settings;

    public FfmpegService(ProjectSettings settings)
    {
        _settings = settings;
    }

    public async Task<string> CreateVideoFromImageAndAudioAsync(
        string imagePath,
        List<string> audioFiles,
        string outputPath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(5);

        var ffmpegPath = Path.Combine(_settings.FfmpegPath, "ffmpeg.exe");
        if (!File.Exists(ffmpegPath))
        {
            ffmpegPath = _settings.FfmpegPath; // Maybe it's the full path
            if (!File.Exists(ffmpegPath))
            {
                throw new Exception($"FFmpeg not found at: {_settings.FfmpegPath}");
            }
        }

        // Step 1: Create concat list file
        progress?.Report(10);
        var tempDir = Path.GetTempPath();
        var concatListPath = Path.Combine(tempDir, $"concat_{Guid.NewGuid()}.txt");
        var mergedAudioPath = Path.Combine(tempDir, $"merged_{Guid.NewGuid()}.wav");

        try
        {
            // Write concat list
            var concatContent = string.Join("\n", audioFiles.Select(f => $"file '{f.Replace("'", "'\\''")}'"));
            await File.WriteAllTextAsync(concatListPath, concatContent, cancellationToken);

            progress?.Report(20);

            // Step 2: Concatenate audio files
            await RunFfmpegAsync(
                ffmpegPath,
                $"-f concat -safe 0 -i \"{concatListPath}\" -c copy \"{mergedAudioPath}\"",
                cancellationToken);

            progress?.Report(50);

            // Get total duration for progress tracking
            var totalDuration = GetAudioDuration(mergedAudioPath);

            // Step 3: Create video with static image and audio
            string videoArgs;
            if (_settings.UseGpuEncoding)
            {
                // Use NVIDIA NVENC for GPU encoding (faster on RTX cards)
                videoArgs = $"-loop 1 -i \"{imagePath}\" -i \"{mergedAudioPath}\" " +
                    $"-c:v h264_nvenc -preset p1 -tune hq -rc vbr -cq 23 " +
                    $"-c:a aac -b:a 192k -pix_fmt yuv420p -shortest -y \"{outputPath}\"";
            }
            else
            {
                // Use CPU encoding with ultrafast preset
                videoArgs = $"-loop 1 -i \"{imagePath}\" -i \"{mergedAudioPath}\" " +
                    $"-c:v libx264 -preset ultrafast -tune stillimage -crf 23 " +
                    $"-c:a aac -b:a 192k -pix_fmt yuv420p -shortest -y \"{outputPath}\"";
            }

            await RunFfmpegWithProgressAsync(ffmpegPath, videoArgs, totalDuration, progress, 50, 99, cancellationToken);

            progress?.Report(100);

            return outputPath;
        }
        finally
        {
            // Cleanup temp files
            try { File.Delete(concatListPath); } catch { }
            try { File.Delete(mergedAudioPath); } catch { }
        }
    }

    private async Task RunFfmpegAsync(string ffmpegPath, string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();

        var errorOutput = await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new Exception($"FFmpeg failed (exit code {process.ExitCode}): {errorOutput}");
        }
    }

    private async Task RunFfmpegWithProgressAsync(
        string ffmpegPath, string arguments,
        TimeSpan totalDuration, IProgress<int>? progress,
        int progressStart, int progressEnd,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();

        var errorBuilder = new StringBuilder();
        var timeRegex = new Regex(@"time=(\d+):(\d+):(\d+)\.(\d+)");

        string? line;
        while ((line = await process.StandardError.ReadLineAsync(cancellationToken)) != null)
        {
            errorBuilder.AppendLine(line);

            var match = timeRegex.Match(line);
            if (match.Success && totalDuration > TimeSpan.Zero)
            {
                var current = new TimeSpan(0,
                    int.Parse(match.Groups[1].Value),
                    int.Parse(match.Groups[2].Value),
                    int.Parse(match.Groups[3].Value),
                    int.Parse(match.Groups[4].Value) * 10);
                var ratio = Math.Min(current.TotalSeconds / totalDuration.TotalSeconds, 1.0);
                var value = progressStart + (int)(ratio * (progressEnd - progressStart));
                progress?.Report(value);
            }
        }

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new Exception($"FFmpeg failed (exit code {process.ExitCode}): {errorBuilder}");
        }
    }

    public TimeSpan GetAudioDuration(string audioPath)
    {
        try
        {
            using var reader = new AudioFileReader(audioPath);
            return reader.TotalTime;
        }
        catch
        {
            return TimeSpan.Zero;
        }
    }

    public async Task<bool> TestFfmpegAsync(string ffmpegPath)
    {
        try
        {
            var exePath = Path.Combine(ffmpegPath, "ffmpeg.exe");
            if (!File.Exists(exePath))
            {
                exePath = ffmpegPath;
                if (!File.Exists(exePath))
                {
                    return false;
                }
            }

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private string GetFfmpegExePath()
    {
        var ffmpegPath = Path.Combine(_settings.FfmpegPath, "ffmpeg.exe");
        if (File.Exists(ffmpegPath)) return ffmpegPath;
        ffmpegPath = _settings.FfmpegPath;
        if (File.Exists(ffmpegPath)) return ffmpegPath;
        throw new Exception($"FFmpeg not found at: {_settings.FfmpegPath}");
    }

    private string GetFfprobePath()
    {
        var dir = Path.GetDirectoryName(GetFfmpegExePath()) ?? "";
        var ffprobePath = Path.Combine(dir, "ffprobe.exe");
        if (File.Exists(ffprobePath)) return ffprobePath;
        throw new Exception($"ffprobe not found at: {dir}");
    }

    private async Task<double> GetClipDurationSeconds(string videoPath, CancellationToken cancellationToken)
    {
        var ffprobePath = GetFfprobePath();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = $"-v error -show_entries format=duration -of csv=p=0 \"{videoPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (double.TryParse(output.Trim(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var duration))
            return duration;
        return 0;
    }

    public async Task<string> CreateMultiImageVideoAsync(
        List<(string imagePath, double durationSeconds)> sceneImages,
        List<string> audioFiles,
        string outputPath,
        bool useGpu = false,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default,
        BgmOptions? bgmOptions = null)
    {
        if (sceneImages.Count == 0)
            throw new Exception("No scene images provided");

        var ffmpegPath = GetFfmpegExePath();
        var tempDir = Path.Combine(Path.GetTempPath(), $"multiimg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        const int fps = 30;
        const double transitionDur = 1.0;

        try
        {
            progress?.Report(5);

            // === Pass 1: Create zoompan clips per image (parallel) ===
            var clipPaths = new string[sceneImages.Count];
            var clipSemaphore = new SemaphoreSlim(4); // Limit to 4 concurrent FFmpeg processes
            int clipsCompleted = 0;

            var clipTasks = sceneImages.Select((scene, i) => Task.Run(async () =>
            {
                await clipSemaphore.WaitAsync(cancellationToken);
                try
                {
                    var (imagePath, duration) = scene;
                    // Add overlap for transition (except last clip)
                    var clipDuration = i < sceneImages.Count - 1
                        ? duration + transitionDur
                        : duration;

                    var frames = (int)(clipDuration * fps);
                    var clipPath = Path.Combine(tempDir, $"clip_{i:D3}.mp4");

                    // Alternate zoom-in and zoom-out (first image = no zoom)
                    string zoomExpr;
                    if (i == 0)
                    {
                        // First image: static, no zoom
                        zoomExpr = "1.0";
                    }
                    else if (i % 2 == 1)
                    {
                        // Odd: Zoom out: 1.15 -> 1.0
                        zoomExpr = $"if(eq(on\\,1)\\,1.15\\,zoom-0.15/{frames})";
                    }
                    else
                    {
                        // Even: Zoom in: 1.0 -> 1.15
                        zoomExpr = $"if(eq(on\\,1)\\,1.0\\,zoom+0.15/{frames})";
                    }

                    var args = $"-loop 1 -i \"{imagePath}\" " +
                        $"-vf \"scale=4000:-1,zoompan=z='{zoomExpr}':d={frames}:s=1920x1080:fps={fps}\" " +
                        $"-c:v libx264 -preset ultrafast -pix_fmt yuv420p -t {clipDuration.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)} " +
                        $"-y \"{clipPath}\"";

                    await RunFfmpegAsync(ffmpegPath, args, cancellationToken);
                    clipPaths[i] = clipPath;

                    var done = Interlocked.Increment(ref clipsCompleted);
                    var clipProgress = 5 + (int)(45.0 * done / sceneImages.Count);
                    progress?.Report(clipProgress);
                }
                finally
                {
                    clipSemaphore.Release();
                }
            }, cancellationToken)).ToArray();

            await Task.WhenAll(clipTasks);
            clipSemaphore.Dispose();

            // === Pass 2: Merge audio ===
            progress?.Report(50);
            var mergedAudioPath = Path.Combine(tempDir, "merged_audio.wav");
            var concatListPath = Path.Combine(tempDir, "audio_concat.txt");
            var concatContent = string.Join("\n", audioFiles.Select(f => $"file '{f.Replace("'", "'\\''")}'"));
            await File.WriteAllTextAsync(concatListPath, concatContent, cancellationToken);

            await RunFfmpegAsync(ffmpegPath,
                $"-f concat -safe 0 -i \"{concatListPath}\" -c copy \"{mergedAudioPath}\"",
                cancellationToken);

            progress?.Report(55);

            // === Pass 2.5: Mix BGM with narration (if enabled) ===
            var finalAudioPath = mergedAudioPath;
            if (bgmOptions != null && File.Exists(bgmOptions.FilePath))
            {
                finalAudioPath = await MixBgmWithNarrationAsync(
                    ffmpegPath, mergedAudioPath, bgmOptions, tempDir,
                    progress, 55, 65, cancellationToken);
            }

            progress?.Report(65);

            // === Pass 3: xfade combine clips + audio ===
            if (clipPaths.Length == 1)
            {
                // Single clip: just add audio
                var args = $"-i \"{clipPaths[0]}\" -i \"{finalAudioPath}\" " +
                    $"-c:v copy -c:a aac -b:a 192k -shortest -y \"{outputPath}\"";
                await RunFfmpegAsync(ffmpegPath, args, cancellationToken);
            }
            else
            {
                // Build xfade filter chain
                var filterScript = BuildXfadeFilterScript(clipPaths, sceneImages, transitionDur);
                var filterScriptPath = Path.Combine(tempDir, "filter_script.txt");
                await File.WriteAllTextAsync(filterScriptPath, filterScript, cancellationToken);

                // Build input args
                var inputArgs = string.Join(" ", clipPaths.Select(p => $"-i \"{p}\""));

                var totalDuration = GetAudioDuration(finalAudioPath);

                var encodeArgs = useGpu
                    ? "-c:v h264_nvenc -preset p1 -tune hq -rc vbr -cq 23"
                    : "-c:v libx264 -preset fast -crf 23";

                // Read filter content and pass via -/filter_complex (FFmpeg 8.x compatible)
                // -/filter_complex reads filter from file, replacing deprecated -filter_complex_script
                var args = $"{inputArgs} -i \"{finalAudioPath}\" " +
                    $"-/filter_complex \"{filterScriptPath}\" " +
                    $"-map \"[vout]\" -map {clipPaths.Length}:a " +
                    $"{encodeArgs} -c:a aac -b:a 192k -pix_fmt yuv420p -shortest -y \"{outputPath}\"";

                await RunFfmpegWithProgressAsync(ffmpegPath, args, totalDuration, progress, 65, 99, cancellationToken);
            }

            progress?.Report(100);
            return outputPath;
        }
        finally
        {
            // Cleanup temp directory
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private async Task<string> MixBgmWithNarrationAsync(
        string ffmpegPath,
        string narrationAudioPath,
        BgmOptions bgm,
        string tempDir,
        IProgress<int>? progress,
        int progressStart,
        int progressEnd,
        CancellationToken cancellationToken)
    {
        var mixedAudioPath = Path.Combine(tempDir, "mixed_audio.wav");

        // Get narration duration to calculate fade-out start time
        var narrationDuration = GetAudioDuration(narrationAudioPath);
        var totalSeconds = narrationDuration.TotalSeconds;

        if (totalSeconds <= 0)
        {
            // Cannot determine duration; skip BGM mixing
            return narrationAudioPath;
        }

        var fadeOutStart = Math.Max(0, totalSeconds - bgm.FadeOutSeconds);

        // Format numbers for FFmpeg (invariant culture)
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var vol = bgm.Volume.ToString("F3", inv);
        var fadeInDur = bgm.FadeInSeconds.ToString("F1", inv);
        var fadeOutDur = bgm.FadeOutSeconds.ToString("F1", inv);
        var fadeOutStartStr = fadeOutStart.ToString("F2", inv);

        // Filter chain:
        // 1. BGM: reduce volume + fade in/out
        // 2. sidechaincompress: narration controls BGM ducking
        // 3. amix: mix narration + ducked BGM
        var filterComplex =
            $"[1:a]volume={vol}," +
            $"afade=t=in:st=0:d={fadeInDur}," +
            $"afade=t=out:st={fadeOutStartStr}:d={fadeOutDur}[bgm];" +
            $"[bgm][0:a]sidechaincompress=threshold=0.02:ratio=3:attack=200:release=1000:detection=rms[ducked];" +
            $"[0:a][ducked]amix=inputs=2:duration=first:normalize=0[out]";

        var args = $"-i \"{narrationAudioPath}\" -stream_loop -1 -i \"{bgm.FilePath}\" " +
            $"-filter_complex \"{filterComplex}\" " +
            $"-map \"[out]\" -c:a pcm_s16le -y \"{mixedAudioPath}\"";

        AppLogger.Log($"BGM mix: vol={vol}, fadeIn={fadeInDur}s, fadeOut={fadeOutDur}s, duration={totalSeconds:F1}s");

        await RunFfmpegWithProgressAsync(
            ffmpegPath, args, narrationDuration, progress,
            progressStart, progressEnd, cancellationToken);

        return mixedAudioPath;
    }

    private string BuildXfadeFilterScript(
        IReadOnlyList<string> clipPaths,
        List<(string imagePath, double durationSeconds)> sceneImages,
        double transitionDur)
    {
        // Build xfade chain using sequential labels: [tmp0], [tmp1], ..., [vout]
        var sb = new StringBuilder();
        var cumulativeDuration = 0.0;
        var transStr = transitionDur.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        for (int i = 1; i < clipPaths.Count; i++)
        {
            cumulativeDuration += sceneImages[i - 1].durationSeconds;
            var offset = cumulativeDuration.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

            // Input: first iteration uses [0:v], subsequent use previous [tmpN]
            var inputLabel = i == 1 ? "[0:v]" : $"[tmp{i - 2}]";
            // Output: last iteration outputs [vout], others output [tmpN]
            var outputLabel = i == clipPaths.Count - 1 ? "[vout]" : $"[tmp{i - 1}]";

            if (i > 1) sb.Append(';');
            sb.Append($"{inputLabel}[{i}:v]xfade=transition=fade:duration={transStr}:offset={offset}{outputLabel}");
        }

        return sb.ToString();
    }

    public async Task<bool> TestNvencAsync()
    {
        try
        {
            var ffmpegPath = Path.Combine(_settings.FfmpegPath, "ffmpeg.exe");
            if (!File.Exists(ffmpegPath))
            {
                ffmpegPath = _settings.FfmpegPath;
                if (!File.Exists(ffmpegPath))
                {
                    return false;
                }
            }

            // Test if h264_nvenc encoder is available
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-hide_banner -encoders",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return output.Contains("h264_nvenc");
        }
        catch
        {
            return false;
        }
    }
}
