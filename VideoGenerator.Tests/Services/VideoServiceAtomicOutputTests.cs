using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests;

public sealed class VideoServiceAtomicOutputTests
{
    [Fact]
    public void PublishesCompletedVideoAndReplacesPreviousOutput()
    {
        string directory = Directory.CreateTempSubdirectory("VideoGenerator.AtomicOutput.").FullName;
        try
        {
            string outputPath = Path.Combine(directory, "video.mp4");
            string temporaryPath = Path.Combine(directory, "video.mp4.partial.mp4");
            File.WriteAllText(outputPath, "previous video");
            File.WriteAllText(temporaryPath, "completed video");

            VideoService.PublishRenderedVideo(temporaryPath, outputPath, CancellationToken.None);

            Assert.Equal("completed video", File.ReadAllText(outputPath));
            Assert.False(File.Exists(temporaryPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CancellationPreservesPreviousVideo()
    {
        string directory = Directory.CreateTempSubdirectory("VideoGenerator.AtomicOutput.").FullName;
        try
        {
            string outputPath = Path.Combine(directory, "video.mp4");
            string temporaryPath = Path.Combine(directory, "video.mp4.partial.mp4");
            File.WriteAllText(outputPath, "previous video");
            File.WriteAllText(temporaryPath, "unfinished video");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                VideoService.PublishRenderedVideo(temporaryPath, outputPath, cancellation.Token));

            Assert.Equal("previous video", File.ReadAllText(outputPath));
            Assert.True(File.Exists(temporaryPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrEmptyRenderPreservesPreviousVideo(bool createEmptyTemporaryFile)
    {
        string directory = Directory.CreateTempSubdirectory("VideoGenerator.AtomicOutput.").FullName;
        try
        {
            string outputPath = Path.Combine(directory, "video.mp4");
            string temporaryPath = Path.Combine(directory, "video.mp4.partial.mp4");
            File.WriteAllText(outputPath, "previous video");
            if (createEmptyTemporaryFile)
                File.WriteAllBytes(temporaryPath, Array.Empty<byte>());

            Assert.Throws<IOException>(() =>
                VideoService.PublishRenderedVideo(temporaryPath, outputPath, CancellationToken.None));

            Assert.Equal("previous video", File.ReadAllText(outputPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
