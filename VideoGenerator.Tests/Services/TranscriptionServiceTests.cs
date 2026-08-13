using System.Net.Http;
using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests;

public sealed class TranscriptionServiceTests
{
    [Fact]
    public async Task MissingAudioReturnsEmptyWithoutPreparingWhisper()
    {
        var logger = new LogService();
        using var httpClient = new HttpClient();
        var service = new TranscriptionService(
            logger,
            new VideoService(logger, Path.Combine(Path.GetTempPath(), "VideoGenerator.Tests", Guid.NewGuid().ToString("N"))),
            httpClient,
            new AppSettings());

        string result = await service.TranscribeAudioAsync(
            Path.Combine(Path.GetTempPath(), "VideoGenerator.Tests", Guid.NewGuid().ToString("N"), "missing.ogg"));

        Assert.Empty(result);
    }

    [Fact]
    public async Task BatchTranscriptionReportsCallbacksForMissingAudio()
    {
        var logger = new LogService();
        using var httpClient = new HttpClient();
        var service = new TranscriptionService(
            logger,
            new VideoService(logger, Path.Combine(Path.GetTempPath(), "VideoGenerator.Tests", Guid.NewGuid().ToString("N"))),
            httpClient,
            new AppSettings());
        var audioPaths = new[]
        {
            Path.Combine(Path.GetTempPath(), "VideoGenerator.Tests", Guid.NewGuid().ToString("N"), "first.ogg"),
            Path.Combine(Path.GetTempPath(), "VideoGenerator.Tests", Guid.NewGuid().ToString("N"), "second.ogg")
        };
        var started = new List<string>();
        var completed = new List<string>();

        string result = await service.TranscribeAudiosAsync(
            audioPaths,
            started.Add,
            completed.Add,
            CancellationToken.None);

        Assert.Empty(result);
        Assert.Equal(audioPaths, started);
        Assert.Equal(audioPaths, completed);
    }

    [Fact]
    public async Task BatchTranscriptionHonorsCancellationBeforeProcessing()
    {
        var logger = new LogService();
        using var httpClient = new HttpClient();
        var service = new TranscriptionService(
            logger,
            new VideoService(logger, Path.Combine(Path.GetTempPath(), "VideoGenerator.Tests", Guid.NewGuid().ToString("N"))),
            httpClient,
            new AppSettings());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.TranscribeAudiosAsync(
            new[] { "missing.ogg" },
            _ => { },
            _ => { },
            cancellation.Token));
    }
}
