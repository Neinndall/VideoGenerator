using System;
using System.Collections.Generic;
using System.Linq;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Calculates deterministic work budgets for preparation and rendering workflows.
    /// </summary>
    public sealed class ProductionWorkPlanningService
    {
        private readonly AppSettings _settings;

        public ProductionWorkPlanningService(AppSettings settings)
        {
            _settings = settings;
        }

        public PreparationWorkPlan CreatePreparationPlan(IReadOnlyList<PreviewEventModel> pipelineEvents)
        {
            var plan = new PreparationWorkPlan();
            plan.MergeWork = pipelineEvents
                .Where(HasPendingAudioFamilies)
                .SelectMany(pipelineEvent => pipelineEvent.AudioFamilies)
                .Sum(family => family.AudioFiles.Count);

            foreach (PreviewEventModel pipelineEvent in pipelineEvents)
            {
                bool canPrepare = pipelineEvent.Status != "No Audio" && pipelineEvent.ParsedData != null;
                int plannedAudioCount = GetPlannedAudioCount(pipelineEvent);
                bool shouldTranscribe = canPrepare && _settings.EnableTranscriptions && plannedAudioCount > 0 &&
                    (string.IsNullOrEmpty(pipelineEvent.Dialogue) || _settings.ForceBatchRetranscribe);
                int transcriptionWork = shouldTranscribe ? plannedAudioCount : 0;
                int dialogueParts = GetDialoguePartCount(pipelineEvent.Dialogue);
                int imageWork = canPrepare
                    ? plannedAudioCount > 1 && (shouldTranscribe || dialogueParts > 1) ? plannedAudioCount : 1
                    : 0;

                plan.TranscriptionWorkByEvent[pipelineEvent] = transcriptionWork;
                plan.ImageWorkByEvent[pipelineEvent] = imageWork;
                plan.TranscriptionWork += transcriptionWork;
                plan.ImageWork += imageWork;

                if (pipelineEvent.ParsedData != null &&
                    pipelineEvent.ParsedData.IconType != "generic" &&
                    string.IsNullOrEmpty(pipelineEvent.ParsedData.IconPath))
                {
                    plan.IconWork++;
                }
            }

            return plan;
        }

        public RenderWorkPlan CreateRenderPlan(IReadOnlyList<PreviewEventModel> pipelineEvents)
        {
            var plan = new RenderWorkPlan
            {
                MergeWork = pipelineEvents
                    .Where(HasPendingAudioFamilies)
                    .SelectMany(pipelineEvent => pipelineEvent.AudioFamilies)
                    .Sum(family => family.AudioFiles.Count)
            };

            foreach (PreviewEventModel pipelineEvent in pipelineEvents)
            {
                bool canRender = pipelineEvent.Status != "No Audio" &&
                                 pipelineEvent.Status != "Missing Icon" &&
                                 pipelineEvent.ParsedData != null;
                int plannedAudioCount = GetPlannedAudioCount(pipelineEvent);
                int dialogueParts = GetDialoguePartCount(pipelineEvent.Dialogue);
                int imageWork = canRender && plannedAudioCount > 0
                    ? dialogueParts > 1 && plannedAudioCount > 1 ? plannedAudioCount : 1
                    : 0;

                plan.ImageWork += imageWork;
                if (imageWork > 0)
                {
                    plan.VideoWork += VideoService.CalculateVideoWorkUnits(
                        imageWork,
                        plannedAudioCount,
                        _settings.SilenceDuration);
                }
            }

            return plan;
        }

        private int GetPlannedAudioCount(PreviewEventModel pipelineEvent)
        {
            return _settings.MergeAudioFamilies && pipelineEvent.AudioFamilies.Count > 0
                ? pipelineEvent.DirectAudioFiles.Count + pipelineEvent.AudioFamilies.Count
                : pipelineEvent.AudioFiles.Count;
        }

        private bool HasPendingAudioFamilies(PreviewEventModel pipelineEvent)
        {
            return _settings.MergeAudioFamilies && !pipelineEvent.AreAudioFamiliesMerged;
        }

        private static int GetDialoguePartCount(string dialogue)
        {
            return string.IsNullOrEmpty(dialogue)
                ? 0
                : dialogue.Split(new[] { "||" }, StringSplitOptions.None).Length;
        }
    }

    public sealed class PreparationWorkPlan
    {
        public Dictionary<PreviewEventModel, int> TranscriptionWorkByEvent { get; } = new();
        public Dictionary<PreviewEventModel, int> ImageWorkByEvent { get; } = new();
        public int MergeWork { get; set; }
        public int IconWork { get; set; }
        public int TranscriptionWork { get; set; }
        public int ImageWork { get; set; }
        public int TotalWork => MergeWork + IconWork + TranscriptionWork + ImageWork;
    }

    public sealed class RenderWorkPlan
    {
        public int MergeWork { get; set; }
        public int ImageWork { get; set; }
        public int VideoWork { get; set; }
        public int TotalWork => MergeWork + ImageWork + VideoWork;
    }
}
