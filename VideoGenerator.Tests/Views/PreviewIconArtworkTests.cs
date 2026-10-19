using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using VideoGenerator.Views.Helpers;
using Xunit;

namespace VideoGenerator.Tests.Views;

public sealed class PreviewIconArtworkTests
{
    [Fact]
    public void DesignerArtworkIsTransparentAroundADarkTileAndNeverReplacesExistingFile()
    {
        string root = Directory.CreateTempSubdirectory("VG.PreviewIcon.").FullName;
        try
        {
            string path = Path.Combine(root, "dialogue-preview.png");
            Exception? problem = null;
            var thread = new Thread(() =>
            {
                try
                {
                    Assert.Equal(path, PreviewIconArtwork.EnsureCreated(path));
                    using var artwork = Image.Load<Rgba32>(path);
                    Assert.Equal(360, artwork.Width);
                    Assert.Equal(360, artwork.Height);
                    Assert.Equal(0, artwork[0, 0].A);
                    Assert.True(artwork[20, 180].A > 200);
                    Assert.True(artwork[180, 180].A > 200);
                    Assert.True(artwork[20, 180].B > artwork[20, 180].R,
                        "The tile should be dark navy rather than flat violet.");

                    byte[] existing = File.ReadAllBytes(path);
                    Assert.Equal(path, PreviewIconArtwork.EnsureCreated(path));
                    Assert.Equal(existing, File.ReadAllBytes(path));
                }
                catch (Exception ex) { problem = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (problem != null) ExceptionDispatchInfo.Capture(problem).Throw();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
