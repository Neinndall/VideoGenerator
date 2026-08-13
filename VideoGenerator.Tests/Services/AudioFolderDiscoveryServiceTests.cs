using Xunit;

using VideoGenerator.Services;

namespace VideoGenerator.Tests;

public sealed class AudioFolderDiscoveryServiceTests
{
    [Fact]
    public void DiscoversParentEventAndIgnoresTechnicalCastFolders()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.Tests.").FullName;

        try
        {
            string eventFolder = Path.Combine(root, "Play_vo_Ahri_Kill3DGeneral");
            string familyFolder = Path.Combine(eventFolder, "[Voice] Main");
            Directory.CreateDirectory(familyFolder);
            File.WriteAllBytes(Path.Combine(familyFolder, "line.ogg"), Array.Empty<byte>());

            string technicalFolder = Path.Combine(root, "Play_vo_Ahri_cast3D");
            Directory.CreateDirectory(technicalFolder);
            File.WriteAllBytes(Path.Combine(technicalFolder, "ignored.ogg"), Array.Empty<byte>());

            var service = new AudioFolderDiscoveryService();
            var folders = service.GetEventFolders(root);

            Assert.Contains(eventFolder, folders, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(technicalFolder, folders, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(familyFolder, folders, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
