using FFMpegCore;
using System.Diagnostics;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using Xunit;

namespace VideoGenerator.Tests;

public sealed class VideoTimelineExportIntegrationTests
{
    [Fact]
    public async Task RealFfmpegJoinsDifferentVideoFormatsAndMixesMusic()
    {
        string tempDir = Directory.CreateTempSubdirectory("VideoGenerator.Montage.").FullName;
        try
        {
            var videoService = new VideoService(new LogService());
            await videoService.EnsureBinariesReadyAsync();
            string ffmpeg = Path.Combine(GlobalFFOptions.Current.BinaryFolder, "ffmpeg.exe");
            string intro = Path.Combine(tempDir, "intro clip.mp4");
            string silent = Path.Combine(tempDir, "silent clip.mp4");
            string music = Path.Combine(tempDir, "background.wav");
            string output = Path.Combine(tempDir, "final montage.mp4");

            await RunAsync(ffmpeg, "-f", "lavfi", "-i", "color=c=blue:s=320x180:r=24:d=1",
                "-f", "lavfi", "-i", "sine=frequency=440:duration=1", "-shortest",
                "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", "-y", intro);
            await RunAsync(ffmpeg, "-f", "lavfi", "-i", "color=c=red:s=400x220:r=30:d=1",
                "-an", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-y", silent);
            await RunAsync(ffmpeg, "-f", "lavfi", "-i", "sine=frequency=220:duration=3",
                "-c:a", "pcm_s16le", "-y", music);

            var project = new VideoTimelineProject
            {
                Name = "Real render test",
                MusicPath = music,
                MusicVolume = 0.2,
                Clips =
                [
                    new VideoTimelineClip { SourcePath = intro, DurationSeconds = 0.6, DisplayName = "Intro" },
                    new VideoTimelineClip { SourcePath = silent, TrimStartSeconds = 0.1,
                        DurationSeconds = 0.6, DisplayName = "Silent" }
                ]
            };
            var renderer = new VideoTimelineRenderService(videoService);
            var preset = new TimelineExportPreset("Test 360p", 640, 360, 24, 28);
            await renderer.ExportAsync(project, output, preset);

            Assert.True(File.Exists(output));
            Assert.True(new FileInfo(output).Length > 0);
            Assert.True(File.Exists(intro)); // Original source clips are never modified.
            Assert.True(File.Exists(silent));
            var result = await renderer.InspectClipAsync(output);
            Assert.True(result.HasVideo);
            Assert.True(result.HasAudio);
            Assert.InRange(result.DurationSeconds, 1.10, 1.40);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static async Task RunAsync(string executable, params string[] args)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-hide_banner");
        start.ArgumentList.Add("-loglevel");
        start.ArgumentList.Add("error");
        foreach (string arg in args)
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("Could not start FFmpeg test fixture.");
        Task<string> errors = process.StandardError.ReadToEndAsync();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        await output;
        string error = await errors;
        Assert.True(process.ExitCode == 0, $"FFmpeg fixture failed: {error}");
    }
}
