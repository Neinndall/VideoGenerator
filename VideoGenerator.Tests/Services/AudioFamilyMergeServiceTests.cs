using System.Reflection;
using System.Threading;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using Xunit;

namespace VideoGenerator.Tests;

public sealed class AudioFamilyMergeServiceTests
{
    [Fact]
    public async Task MergeSkipsAudioFamiliesWhenTheSettingIsDisabled()
    {
        var pipelineEvent = CreateEventWithFamily();
        var statuses = new List<string>();
        var mergedFamilies = new List<AudioFamilyModel>();
        var service = CreateService(CreateSettings(mergeAudioFamilies: false));

        await service.MergeAsync(
            pipelineEvent,
            CancellationToken.None,
            mergedFamilies.Add,
            statuses.Add);

        Assert.False(pipelineEvent.AreAudioFamiliesMerged);
        Assert.Equal(new[] { "direct.ogg" }, pipelineEvent.AudioFiles);
        Assert.Empty(mergedFamilies);
        Assert.Empty(statuses);
    }

    [Fact]
    public void IsPendingRequiresEnabledUnmergedAudioFamilies()
    {
        var pipelineEvent = CreateEventWithFamily();
        var service = CreateService(CreateSettings(mergeAudioFamilies: true));

        Assert.True(service.IsPending(pipelineEvent));

        pipelineEvent.AreAudioFamiliesMerged = true;
        Assert.False(service.IsPending(pipelineEvent));

        pipelineEvent.AreAudioFamiliesMerged = false;
        pipelineEvent.AudioFamilies.Clear();
        Assert.False(service.IsPending(pipelineEvent));
    }

    [Fact]
    public async Task MergeHonorsCancellationBeforeStartingAudioProcessing()
    {
        var pipelineEvent = CreateEventWithFamily();
        var statuses = new List<string>();
        var mergedFamilies = new List<AudioFamilyModel>();
        var service = CreateService(CreateSettings(mergeAudioFamilies: true));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.MergeAsync(
            pipelineEvent,
            cancellation.Token,
            mergedFamilies.Add,
            statuses.Add));

        Assert.False(pipelineEvent.AreAudioFamiliesMerged);
        Assert.Equal(new[] { "direct.ogg" }, pipelineEvent.AudioFiles);
        Assert.Empty(mergedFamilies);
        Assert.Empty(statuses);
    }

    private static AudioFamilyMergeService CreateService(AppSettings settings)
    {
        string cacheDirectory = Path.Combine(
            Path.GetTempPath(),
            "VideoGenerator.Tests",
            Guid.NewGuid().ToString("N"));
        return new AudioFamilyMergeService(
            new VideoService(new LogService(), cacheDirectory),
            settings);
    }

    private static PreviewEventModel CreateEventWithFamily()
    {
        return new PreviewEventModel
        {
            FolderPath = Path.GetTempPath(),
            FolderName = "Play_vo_Aatrox_Kill3DGeneral",
            DirectAudioFiles = new List<string> { "direct.ogg" },
            AudioFiles = new List<string> { "direct.ogg" },
            AudioFamilies = new List<AudioFamilyModel>
            {
                new()
                {
                    Name = "[Voice] Main",
                    AudioFiles = new List<string> { "family.ogg" }
                }
            }
        };
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
}
