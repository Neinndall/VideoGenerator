using VideoGenerator.Views.Models;
using Xunit;

namespace VideoGenerator.Tests;

public sealed class VideoTimelineProjectTests
{
    [Fact]
    public async Task SavesAndLoadsEditablePathsTrimsMusicAndClipOrder()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.Timeline.").FullName;
        try
        {
            string path = Path.Combine(root, "draft.timeline.json");
            var project = new VideoTimelineProject
            {
                Name = "Montaje: campeón",
                MusicPath = @"media\música con espacios.ogg",
                MusicVolume = 0.4,
                Clips =
                [
                    new VideoTimelineClip { SourcePath = @"media\apertura.mp4", TrimStartSeconds = 1.25,
                        DurationSeconds = 4.5, DisplayName = "Apertura" },
                    new VideoTimelineClip { SourcePath = @"media\clip '2'.mp4", TrimStartSeconds = 0,
                        DurationSeconds = 2.75, DisplayName = "Final" }
                ]
            };

            await project.SaveAsync(path);
            VideoTimelineProject loaded = await VideoTimelineProject.LoadAsync(path);

            Assert.Equal("Montaje: campeón", loaded.Name);
            Assert.Equal(0.4, loaded.MusicVolume);
            Assert.Equal(project.MusicPath, loaded.MusicPath);
            Assert.Equal(project.Clips.Select(clip => clip.SourcePath), loaded.Clips.Select(clip => clip.SourcePath));
            Assert.Equal(new[] { 1.25, 0d }, loaded.Clips.Select(clip => clip.TrimStartSeconds));
            Assert.Equal(new[] { 4.5, 2.75 }, loaded.Clips.Select(clip => clip.DurationSeconds));
            Assert.Equal(new[] { "Apertura", "Final" }, loaded.Clips.Select(clip => clip.DisplayName));
            Assert.Equal(path, loaded.ProjectFilePath);
            Assert.Equal(Path.Combine(root, "media", "apertura.mp4"),
                loaded.ResolveMediaPath(loaded.Clips[0].SourcePath));
            Assert.DoesNotContain("ProjectFilePath", await File.ReadAllTextAsync(path));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task InvalidSavePreservesExistingProjectJsonAndNeverLeavesTempFiles()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.Timeline.").FullName;
        try
        {
            string path = Path.Combine(root, "existing.json");
            const string old = "{\"Name\":\"Original edit\"}";
            await File.WriteAllTextAsync(path, old);
            var project = new VideoTimelineProject
            {
                Name = "Incompleto",
                Clips = [new VideoTimelineClip { SourcePath = "source.mp4", DurationSeconds = -3 }]
            };

            await Assert.ThrowsAsync<InvalidDataException>(() => project.SaveAsync(path));
            Assert.Equal(old, await File.ReadAllTextAsync(path));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));

            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            project.Clips[0].DurationSeconds = 3;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => project.SaveAsync(path, cancelled.Token));
            Assert.Equal(old, await File.ReadAllTextAsync(path));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("not a JSON file")]
    [InlineData("null")]
    [InlineData("{\"Name\":\"X\",\"MusicVolume\":2}")]
    [InlineData("{\"Name\":\"X\",\"Clips\":[null]}")]
    public async Task InvalidJsonOrInvalidProjectFailsWithoutRewritingSource(string json)
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.Timeline.").FullName;
        try
        {
            string path = Path.Combine(root, "invalid.json");
            await File.WriteAllTextAsync(path, json);

            await Assert.ThrowsAsync<InvalidDataException>(() => VideoTimelineProject.LoadAsync(path));
            Assert.Equal(json, await File.ReadAllTextAsync(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void RejectsInvalidMusicVolumes(double volume)
    {
        var project = new VideoTimelineProject { MusicVolume = volume };
        Assert.Contains(project.Validate(), message => message.Contains("volumen", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PresetsUseValidH264DimensionsAndQualityRanges()
    {
        Assert.NotEmpty(TimelineExportPreset.Presets);
        Assert.Contains(TimelineExportPreset.Presets, preset => preset.Width == 1920 && preset.Height == 1080);
        Assert.All(TimelineExportPreset.Presets, preset => Assert.Empty(preset.Validate()));
        Assert.NotEmpty(new TimelineExportPreset("Roto", 1919, 1080, 150, 70).Validate());
    }
}
