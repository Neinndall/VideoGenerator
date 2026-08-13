using System.Windows.Media.Imaging;
using VideoGenerator.Models;
using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class PreviewImageServiceIntegrationTests
{
    [Fact]
    public async Task CreatesFrozenPreviewFromEmbeddedBackground()
    {
        var service = new PreviewImageService(
            new ImageGenerator(new LogService()),
            new AppSettings());

        var image = await service.CreateAsync(new ParsedEvent
        {
            OriginalFolder = "Preview_Test",
            DisplayText = "Preview test",
            IconType = "generic"
        }, CancellationToken.None);

        var bitmap = Assert.IsType<BitmapImage>(image);
        Assert.True(bitmap.IsFrozen);
        Assert.Equal(1920, bitmap.PixelWidth);
        Assert.Equal(1080, bitmap.PixelHeight);
    }

    [Fact]
    public async Task CreatePreviewHonorsCancellationBeforeRendering()
    {
        var service = new PreviewImageService(
            new ImageGenerator(new LogService()),
            new AppSettings());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CreateAsync(
            new ParsedEvent
            {
                OriginalFolder = "Canceled_Preview",
                DisplayText = "Canceled preview",
                IconType = "generic"
            },
            cancellation.Token));
    }
}
