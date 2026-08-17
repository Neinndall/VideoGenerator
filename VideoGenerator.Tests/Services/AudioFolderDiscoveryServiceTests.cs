using Xunit;

using VideoGenerator.Services;

namespace VideoGenerator.Tests;

public sealed class AudioFolderDiscoveryServiceTests
{
    [Fact]
    public void DiscoversParentEventAndIncludesCastFolders()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.Tests.").FullName;

        try
        {
            string eventFolder = Path.Combine(root, "Play_vo_Ahri_Kill3DGeneral");
            string familyFolder = Path.Combine(eventFolder, "[Voice] Main");
            Directory.CreateDirectory(familyFolder);
            File.WriteAllBytes(Path.Combine(familyFolder, "line.ogg"), Array.Empty<byte>());

            string cast3DFolder = Path.Combine(root, "Play_vo_Ahri_cast3D");
            Directory.CreateDirectory(cast3DFolder);
            File.WriteAllBytes(Path.Combine(cast3DFolder, "cast3d.ogg"), Array.Empty<byte>());

            string cast2DFolder = Path.Combine(root, "Play_vo_Ahri_cast2D");
            Directory.CreateDirectory(cast2DFolder);
            File.WriteAllBytes(Path.Combine(cast2DFolder, "cast2d.ogg"), Array.Empty<byte>());

            var service = new AudioFolderDiscoveryService();
            var folders = service.GetEventFolders(root);

            Assert.Contains(eventFolder, folders, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(cast3DFolder, folders, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(cast2DFolder, folders, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(familyFolder, folders, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
