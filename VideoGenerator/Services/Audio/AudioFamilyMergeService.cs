using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Merges bracketed audio families into their cached render tracks.
    /// Progress presentation belongs to the calling workflow.
    /// </summary>
    public sealed class AudioFamilyMergeService
    {
        private readonly VideoService _videoService;
        private readonly AppSettings _settings;

        public AudioFamilyMergeService(VideoService videoService, AppSettings settings)
        {
            _videoService = videoService;
            _settings = settings;
        }

        public async Task MergeAsync(
            PreviewEventModel pipelineEvent,
            CancellationToken cancellationToken,
            Action<AudioFamilyModel> onFamilyMerged = null,
            Action<string> reportStatus = null)
        {
            if (!_settings.MergeAudioFamilies ||
                pipelineEvent.AudioFamilies.Count == 0 ||
                pipelineEvent.AreAudioFamiliesMerged)
            {
                return;
            }

            var mergedTracks = new List<string>(pipelineEvent.DirectAudioFiles);
            foreach (AudioFamilyModel family in pipelineEvent.AudioFamilies)
            {
                cancellationToken.ThrowIfCancellationRequested();
                reportStatus?.Invoke($"Merging audio family: {family.Name}");

                string mergedPath = await _videoService.MergeAudioFamilyAsync(
                    family.AudioFiles,
                    pipelineEvent.FolderName,
                    family.Name,
                    pipelineEvent.FolderPath,
                    cancellationToken);
                mergedTracks.Add(mergedPath);
                onFamilyMerged?.Invoke(family);
            }

            pipelineEvent.AudioFiles = mergedTracks;
            pipelineEvent.AreAudioFamiliesMerged = true;
        }

        public async Task MergeAsync(
            IReadOnlyList<PreviewEventModel> pipelineEvents,
            CancellationToken cancellationToken,
            Action<AudioFamilyModel> onFamilyMerged = null,
            Action<string> reportStatus = null)
        {
            foreach (PreviewEventModel pipelineEvent in pipelineEvents.Where(IsPending))
            {
                await MergeAsync(pipelineEvent, cancellationToken, onFamilyMerged, reportStatus);
            }
        }

        public bool IsPending(PreviewEventModel pipelineEvent)
        {
            return _settings.MergeAudioFamilies &&
                   pipelineEvent.AudioFamilies.Count > 0 &&
                   !pipelineEvent.AreAudioFamiliesMerged;
        }
    }
}
