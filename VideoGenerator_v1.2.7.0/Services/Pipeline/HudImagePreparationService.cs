using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VideoGenerator.Models;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Ensures the HUD images required by a pipeline event exist for preparation or rendering.
    /// </summary>
    public sealed class HudImagePreparationService
    {
        private readonly ImageGenerator _imageGenerator;
        private readonly AppSettings _settings;

        public HudImagePreparationService(ImageGenerator imageGenerator, AppSettings settings)
        {
            _imageGenerator = imageGenerator;
            _settings = settings;
        }

        public async Task<IReadOnlyList<string>> PrepareAsync(
            PreviewEventModel pipelineEvent,
            string dialogue,
            bool reuseExistingImages,
            CancellationToken cancellationToken,
            Action<string> reportStatus = null,
            Action<int, string> reportProgress = null)
        {
            var imagePaths = new List<string>();
            string[] dialogueParts = SplitDialogue(dialogue);
            int imageCount = dialogueParts.Length > 1 && pipelineEvent.AudioFiles.Count > 1
                ? pipelineEvent.AudioFiles.Count
                : 1;
            string outputDirectory = Path.Combine(AppConfig.OutputImagesDir, pipelineEvent.CharacterName);

            for (int index = 0; index < imageCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool segmented = imageCount > 1;
                string suffix = segmented ? $"part_{index}" : string.Empty;
                string expectedPath = Path.Combine(
                    outputDirectory,
                    segmented
                        ? $"{pipelineEvent.FolderName}_part_{index}.png"
                        : $"{pipelineEvent.FolderName}.png");

                reportStatus?.Invoke(segmented
                    ? $"Preparing HUD image: {pipelineEvent.FolderName}"
                    : $"Preparing HUD image for {pipelineEvent.FolderName}");

                if (!reuseExistingImages || !File.Exists(expectedPath))
                {
                    expectedPath = await GenerateAsync(
                        pipelineEvent,
                        segmented
                            ? index < dialogueParts.Length ? dialogueParts[index] : string.Empty
                            : dialogue,
                        suffix,
                        cancellationToken);
                }

                imagePaths.Add(expectedPath);
                reportProgress?.Invoke(
                    1,
                    segmented
                        ? $"Prepared image: {pipelineEvent.FolderName} ({index + 1}/{imageCount})"
                        : $"Prepared image: {pipelineEvent.FolderName}");
            }

            return imagePaths;
        }

        public static int GetRequiredImageCount(PreviewEventModel pipelineEvent, string dialogue)
        {
            string[] dialogueParts = SplitDialogue(dialogue);
            return dialogueParts.Length > 1 && pipelineEvent.AudioFiles.Count > 1
                ? pipelineEvent.AudioFiles.Count
                : 1;
        }

        private async Task<string> GenerateAsync(
            PreviewEventModel pipelineEvent,
            string dialogue,
            string suffix,
            CancellationToken cancellationToken)
        {
            string originalDialogue = pipelineEvent.ParsedData.Dialogue;
            try
            {
                pipelineEvent.ParsedData.Dialogue = dialogue;
                return await _imageGenerator.CreateImageAsync(
                    pipelineEvent.ParsedData,
                    _settings.SelectedFontName,
                    _settings.CustomBackgroundPath,
                    _settings.TextVerticalOffset,
                    suffix,
                    pipelineEvent.CharacterName,
                    cancellationToken);
            }
            finally
            {
                pipelineEvent.ParsedData.Dialogue = originalDialogue;
            }
        }

        private static string[] SplitDialogue(string dialogue)
        {
            return string.IsNullOrEmpty(dialogue)
                ? Array.Empty<string>()
                : dialogue.Split(new[] { "||" }, StringSplitOptions.None)
                    .Select(part => part.Trim())
                    .ToArray();
        }
    }
}
