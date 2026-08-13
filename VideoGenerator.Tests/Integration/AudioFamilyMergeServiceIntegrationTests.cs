using System.Reflection;
using System.Text;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using Xunit;

namespace VideoGenerator.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class AudioFamilyMergeServiceIntegrationTests
{
    [Fact]
    public async Task MergesAudioFamilyAndUpdatesPipelineEvent()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.AudioIntegration.").FullName;

        try
        {
            string eventFolder = Path.Combine(root, "Play_vo_Aatrox_Kill3DGeneral");
            Directory.CreateDirectory(eventFolder);

            string directAudioPath = Path.Combine(eventFolder, "direct.wav");
            string firstFamilyAudioPath = Path.Combine(eventFolder, "family-first.wav");
            string secondFamilyAudioPath = Path.Combine(eventFolder, "family-second.wav");
            string cacheDirectory = Path.Combine(root, "cache");

            WriteWave(directAudioPath, 440);
            WriteWave(firstFamilyAudioPath, 660);
            WriteWave(secondFamilyAudioPath, 880);

            var pipelineEvent = new PreviewEventModel
            {
                FolderPath = eventFolder,
                FolderName = "Play_vo_Aatrox_Kill3DGeneral",
                DirectAudioFiles = new List<string> { directAudioPath },
                AudioFiles = new List<string> { directAudioPath },
                AudioFamilies = new List<AudioFamilyModel>
                {
                    new()
                    {
                        Name = "[Voice] Main",
                        AudioFiles = new List<string> { firstFamilyAudioPath, secondFamilyAudioPath }
                    }
                }
            };

            var statuses = new List<string>();
            var mergedFamilies = new List<AudioFamilyModel>();
            var service = new AudioFamilyMergeService(
                new VideoService(new LogService(), cacheDirectory),
                CreateSettings(mergeAudioFamilies: true));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await service.MergeAsync(
                pipelineEvent,
                timeout.Token,
                mergedFamilies.Add,
                statuses.Add);

            Assert.True(pipelineEvent.AreAudioFamiliesMerged);
            Assert.Equal(2, pipelineEvent.AudioFiles.Count);
            Assert.Equal(directAudioPath, pipelineEvent.AudioFiles[0]);
            Assert.True(File.Exists(pipelineEvent.AudioFiles[1]));
            Assert.True(new FileInfo(pipelineEvent.AudioFiles[1]).Length > 0);
            Assert.Same(pipelineEvent.AudioFamilies[0], Assert.Single(mergedFamilies));
            Assert.Contains(statuses, status => status.Contains("[Voice] Main", StringComparison.Ordinal));
            Assert.Empty(Directory.GetFiles(cacheDirectory, "concat_*.txt", SearchOption.AllDirectories));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static AppSettings CreateSettings(bool mergeAudioFamilies)
    {
        var settings = new AppSettings();
        FieldInfo? field = typeof(AppSettings).GetField(
            "_mergeAudioFamilies",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(settings, mergeAudioFamilies);
        return settings;
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
