using VideoGenerator.Views.Models;
using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests;

public sealed class VideoTimelineRenderServiceTests
{
    [Fact]
    public void FilterGraphNormalizesSourcesAndCreatesSilenceForVideoWithoutAudio()
    {
        var preset = new TimelineExportPreset("Prueba", 1280, 720, 30, 22);
        var clips = new[]
        {
            new VideoTimelineRenderService.PreparedClip(
                new VideoTimelineRenderService.RenderClip(@"C:\path with 'quotes'\clip.mp4", 1.25, 4.5), true),
            new VideoTimelineRenderService.PreparedClip(
                new VideoTimelineRenderService.RenderClip(@"C:\path\silent.mp4", 0, 3), false)
        };

        string graph = VideoTimelineRenderService.BuildFilterGraph(clips, preset,
            includeMusic: true, musicVolume: 0.33, timelineDuration: 7.5);

        Assert.Contains("[0:v:0]trim=start=1.25:duration=4.5", graph);
        Assert.Contains("fps=30,scale=1280:720", graph);
        Assert.Contains("pad=1280:720", graph);
        Assert.Contains("[0:a:0]atrim=start=1.25:duration=4.5", graph);
        Assert.Contains("anullsrc=r=48000:cl=stereo,atrim=duration=3", graph);
        Assert.Contains("[v0][a0][v1][a1]concat=n=2:v=1:a=1", graph);
        Assert.Contains("[2:a:0]aresample=48000", graph);
        Assert.Contains("volume=0.33,atrim=duration=7.5", graph);
        Assert.Contains("amix=inputs=2:duration=first", graph);
        Assert.EndsWith("[mergedv]null[outv]", graph);
        Assert.DoesNotContain("quotes", graph);
    }

    [Fact]
    public void FilterGraphWithoutMusicHasValidOutputAudioAndVideoLabels()
    {
        var preset = TimelineExportPreset.Presets[0];
        var clips = new[]
        {
            new VideoTimelineRenderService.PreparedClip(
                new VideoTimelineRenderService.RenderClip("source.mp4", 0, 2), true)
        };

        string graph = VideoTimelineRenderService.BuildFilterGraph(clips, preset,
            includeMusic: false, musicVolume: 0, timelineDuration: 2);

        Assert.Contains("concat=n=1:v=1:a=1", graph);
        Assert.Contains("[mergeda]anull[outa]", graph);
        Assert.DoesNotContain("amix=", graph);
    }

    [Fact]
    public void FilterGraphWithAudioOnlyClipGeneratesBlackVideoBackground()
    {
        var preset = new TimelineExportPreset("Prueba Audio", 1920, 1080, 30, 20);
        var clips = new[]
        {
            new VideoTimelineRenderService.PreparedClip(
                new VideoTimelineRenderService.RenderClip(@"C:\audio\dialogue.ogg", 0, 5),
                HasAudio: true, HasVideo: false)
        };

        string graph = VideoTimelineRenderService.BuildFilterGraph(clips, preset,
            includeMusic: false, musicVolume: 0, timelineDuration: 5);

        Assert.Contains("color=c=black:s=1920x1080:r=30:d=5", graph);
        Assert.Contains("setsar=1,format=yuv420p[v0];", graph);
        Assert.DoesNotContain("[v{i}]", graph);
        Assert.Contains("[0:a:0]atrim=start=0:duration=5", graph);
        Assert.Contains("[v0][a0]concat=n=1:v=1:a=1", graph);
    }
}
