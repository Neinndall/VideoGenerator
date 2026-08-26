using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class VideoServiceIntegrationTests
{
    [Fact]
    public async Task CreatesVideoFromSegmentedImagesAndAudioTracks()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.Integration.").FullName;

        try
        {
            string firstImagePath = Path.Combine(root, "first.png");
            string secondImagePath = Path.Combine(root, "second.png");
            string firstAudioPath = Path.Combine(root, "first.wav");
            string secondAudioPath = Path.Combine(root, "second.wav");
            string outputPath = Path.Combine(root, "result.mp4");
            string cacheDirectory = Path.Combine(root, "cache");

            WritePng(firstImagePath, 38, 24, 74);
            WritePng(secondImagePath, 139, 92, 246);
            WriteWave(firstAudioPath, 440);
            WriteWave(secondAudioPath, 660);

            var workMessages = new List<string>();
            var service = new VideoService(new LogService(), cacheDirectory);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            bool rendered = await service.CreateVideoAsync(
                new List<string> { firstImagePath, secondImagePath },
                new List<string> { firstAudioPath, secondAudioPath },
                outputPath,
                silenceDuration: 0.1,
                dialogue: "First segment || Second segment",
                onWorkCompleted: workMessages.Add,
                cancellationToken: timeout.Token);

            Assert.True(rendered);
            Assert.True(File.Exists(outputPath));
            Assert.True(new FileInfo(outputPath).Length > 0);
            Assert.Contains(workMessages, message => message == "Concatenated final video");

            var analysis = await FFMpegCore.FFProbe.AnalyseAsync(outputPath);
            // first.wav = 0.25s, second.wav = 0.25s, silence = 0.5s -> total should be around 1.0s
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task VerifiesSilenceDurationIsAddedBetweenTracks()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.SilenceTest.").FullName;

        try
        {
            string firstImagePath = Path.Combine(root, "first.png");
            string secondImagePath = Path.Combine(root, "second.png");
            string firstAudioPath = Path.Combine(root, "first.wav");
            string secondAudioPath = Path.Combine(root, "second.wav");
            string outputSegmented = Path.Combine(root, "segmented.mp4");
            string outputSingle = Path.Combine(root, "single.mp4");
            string cacheDirectory = Path.Combine(root, "cache");

            WritePng(firstImagePath, 38, 24, 74);
            WritePng(secondImagePath, 139, 92, 246);
            WriteWave(firstAudioPath, 440); // 0.25s
            WriteWave(secondAudioPath, 660); // 0.25s

            var service = new VideoService(new LogService(), cacheDirectory);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            // Test 1: Segmented (2 tracks, 2s silence on each track -> 0.25 + 2.0 + 0.25 + 2.0 = 4.5s)
            double silence = 2.0;
            bool renderedSegmented = await service.CreateVideoAsync(
                new List<string> { firstImagePath, secondImagePath },
                new List<string> { firstAudioPath, secondAudioPath },
                outputSegmented,
                silenceDuration: silence,
                dialogue: "First || Second",
                cancellationToken: timeout.Token);

            Assert.True(renderedSegmented);
            var analysisSegmented = await FFMpegCore.FFProbe.AnalyseAsync(outputSegmented);
            Assert.True(analysisSegmented.Duration.TotalSeconds >= 4.3 && analysisSegmented.Duration.TotalSeconds <= 4.8,
                $"Segmented duration was {analysisSegmented.Duration.TotalSeconds}s, expected ~4.5s");

            // Test 2: Single image with 2 audio tracks (each padded with 2s silence -> 4.5s)
            bool renderedSingle = await service.CreateVideoAsync(
                firstImagePath,
                new List<string> { firstAudioPath, secondAudioPath },
                outputSingle,
                silenceDuration: silence,
                dialogue: "Single",
                cancellationToken: timeout.Token);

            Assert.True(renderedSingle);
            var analysisSingle = await FFMpegCore.FFProbe.AnalyseAsync(outputSingle);
            Assert.True(analysisSingle.Duration.TotalSeconds >= 4.3 && analysisSingle.Duration.TotalSeconds <= 4.8,
                $"Single image duration was {analysisSingle.Duration.TotalSeconds}s, expected ~4.5s");

            // Test 3: Single audio track with silence (0.25 + 2.0 = 2.25s)
            string outputSingleTrack = Path.Combine(root, "single_track.mp4");
            bool renderedSingleTrack = await service.CreateVideoAsync(
                firstImagePath,
                new List<string> { firstAudioPath },
                outputSingleTrack,
                silenceDuration: silence,
                dialogue: "Single Track",
                cancellationToken: timeout.Token);

            Assert.True(renderedSingleTrack);
            var analysisSingleTrack = await FFMpegCore.FFProbe.AnalyseAsync(outputSingleTrack);
            Assert.True(analysisSingleTrack.Duration.TotalSeconds >= 2.1 && analysisSingleTrack.Duration.TotalSeconds <= 2.5,
                $"Single track duration was {analysisSingleTrack.Duration.TotalSeconds}s, expected ~2.25s");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CreatesVideoFromOneImageAndMultipleAudioTracks()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.Integration.").FullName;

        try
        {
            string imagePath = Path.Combine(root, "single.png");
            string firstAudioPath = Path.Combine(root, "first.wav");
            string secondAudioPath = Path.Combine(root, "second.wav");
            string outputPath = Path.Combine(root, "result.mp4");
            string cacheDirectory = Path.Combine(root, "cache");

            WritePng(imagePath, 72, 44, 128);
            WriteWave(firstAudioPath, 440);
            WriteWave(secondAudioPath, 660);

            var workMessages = new List<string>();
            var service = new VideoService(new LogService(), cacheDirectory);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            bool rendered = await service.CreateVideoAsync(
                imagePath,
                new List<string> { firstAudioPath, secondAudioPath },
                outputPath,
                silenceDuration: 0.1,
                dialogue: "Combined dialogue",
                onWorkCompleted: workMessages.Add,
                cancellationToken: timeout.Token);

            Assert.True(rendered);
            Assert.True(File.Exists(outputPath));
            Assert.True(new FileInfo(outputPath).Length > 0);
            Assert.Contains(workMessages, message => message == "Combined event audio");
            Assert.Contains(workMessages, message => message == "Encoded final video");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CleansTemporaryClipsWhenSegmentedRenderFails()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.Integration.").FullName;

        try
        {
            string imagePath = Path.Combine(root, "source.png");
            string validAudioPath = Path.Combine(root, "valid.wav");
            string missingAudioPath = Path.Combine(root, "missing.wav");
            string outputPath = Path.Combine(root, "result.mp4");
            string cacheDirectory = Path.Combine(root, "cache");

            WritePng(imagePath, 72, 44, 128);
            WriteWave(validAudioPath, 440);

            var service = new VideoService(new LogService(), cacheDirectory);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await Assert.ThrowsAnyAsync<Exception>(() => service.CreateVideoAsync(
                new List<string> { imagePath, imagePath },
                new List<string> { validAudioPath, missingAudioPath },
                outputPath,
                silenceDuration: 0,
                cancellationToken: timeout.Token));

            string[] leftoverClips = Directory.Exists(cacheDirectory)
                ? Directory.GetFiles(cacheDirectory, "temp_clip_*", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();

            Assert.Empty(leftoverClips);
            Assert.False(File.Exists(outputPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CancelsSegmentedRenderAndCleansTemporaryClips()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.Integration.").FullName;

        try
        {
            string imagePath = Path.Combine(root, "source.png");
            string firstAudioPath = Path.Combine(root, "first.wav");
            string secondAudioPath = Path.Combine(root, "second.wav");
            string outputPath = Path.Combine(root, "result.mp4");
            string cacheDirectory = Path.Combine(root, "cache");

            WritePng(imagePath, 72, 44, 128);
            WriteWave(firstAudioPath, 440);
            WriteWave(secondAudioPath, 660);

            var workMessages = new List<string>();
            using var cancellation = new CancellationTokenSource();
            var service = new VideoService(new LogService(), cacheDirectory);

            Action<string> cancelAfterFirstClip = message =>
            {
                workMessages.Add(message);
                if (message == "Created temporary clip 1/2")
                {
                    cancellation.Cancel();
                }
            };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CreateVideoAsync(
                new List<string> { imagePath, imagePath },
                new List<string> { firstAudioPath, secondAudioPath },
                outputPath,
                silenceDuration: 0,
                onWorkCompleted: cancelAfterFirstClip,
                cancellationToken: cancellation.Token));

            Assert.Contains("Created temporary clip 1/2", workMessages);
            string[] leftoverClips = Directory.Exists(cacheDirectory)
                ? Directory.GetFiles(cacheDirectory, "temp_clip_*", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();

            Assert.Empty(leftoverClips);
            Assert.False(File.Exists(outputPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task MergesAudioFamilyWithSilenceDuration()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.FamilySilence.").FullName;

        try
        {
            string firstAudioPath = Path.Combine(root, "first.wav");
            string secondAudioPath = Path.Combine(root, "second.wav");
            string cacheDirectory = Path.Combine(root, "cache");

            WriteWave(firstAudioPath, 440); // 0.25s
            WriteWave(secondAudioPath, 660); // 0.25s

            var service = new VideoService(new LogService(), cacheDirectory);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            double silence = 2.0;
            string mergedWavPath = await service.MergeAudioFamilyAsync(
                new[] { firstAudioPath, secondAudioPath },
                "TestEvent",
                "TestFamily",
                root,
                silenceDuration: silence,
                cancellationToken: timeout.Token);

            Assert.True(File.Exists(mergedWavPath));
            var analysis = await FFMpegCore.FFProbe.AnalyseAsync(mergedWavPath);
            // 0.25s + 0.25s + 2.0s = 2.5s (allow +/- 0.3s)
            Assert.True(analysis.Duration.TotalSeconds >= 2.3 && analysis.Duration.TotalSeconds <= 2.7,
                $"Merged family duration was {analysis.Duration.TotalSeconds}s, expected ~2.5s");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void WritePng(string path, byte red, byte green, byte blue)
    {
        using var image = new Image<Rgba32>(2, 2);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                image[x, y] = new Rgba32(red, green, blue, 255);
            }
        }

        image.SaveAsPng(path);
    }

    private static void WriteWave(string path, double frequency)
    {
        const int sampleRate = 8000;
        const short channels = 1;
        const short bitsPerSample = 16;
        int sampleCount = sampleRate / 4;
        int dataSize = sampleCount * channels * (bitsPerSample / 8);

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * (bitsPerSample / 8));
        writer.Write((short)(channels * (bitsPerSample / 8)));
        writer.Write(bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        for (int sample = 0; sample < sampleCount; sample++)
        {
            double phase = 2 * Math.PI * frequency * sample / sampleRate;
            writer.Write((short)(Math.Sin(phase) * short.MaxValue * 0.15));
        }
    }
}
