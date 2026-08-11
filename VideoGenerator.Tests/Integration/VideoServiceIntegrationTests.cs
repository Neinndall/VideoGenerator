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
