using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Views
{
    /// <summary>
    /// Owns preview refreshes and best-effort icon hydration for the selected pipeline event.
    /// </summary>
    public partial class DashboardView
    {
        private CancellationTokenSource _previewCancellationSource;

        private async Task UpdatePreviewAsync()
        {
            if (_model.SelectedEvent == null) return;

            _previewCancellationSource?.Cancel();
            _previewCancellationSource = new CancellationTokenSource();
            CancellationToken token = _previewCancellationSource.Token;

            try
            {
                await Task.Delay(50, token);
                await GeneratePreviewAsync(_model.SelectedEvent, token);
                if (token.IsCancellationRequested) return;

                PreviewEventModel pipelineEvent = _model.SelectedEvent;
                if (pipelineEvent.ParsedData != null &&
                    pipelineEvent.ParsedData.IconType != "generic" &&
                    string.IsNullOrEmpty(pipelineEvent.ParsedData.IconPath))
                {
                    await ResolveEventIconAsync(pipelineEvent);
                    if (!token.IsCancellationRequested)
                    {
                        await GeneratePreviewAsync(pipelineEvent, token);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // A newer selection superseded this preview request.
            }
        }

        private async Task GeneratePreviewAsync(PreviewEventModel pipelineEvent, CancellationToken token)
        {
            try
            {
                var previewImage = await _previewImageService.CreateAsync(pipelineEvent.ParsedData, token);
                if (!token.IsCancellationRequested && previewImage != null)
                {
                    _model.PreviewImageSource = previewImage;
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    _logger.LogError("Preview generation failed", ex);
                }
            }
        }

        private async Task ResolveEventIconAsync(PreviewEventModel pipelineEvent)
        {
            if (pipelineEvent?.ParsedData == null ||
                pipelineEvent.ParsedData.IconType == "generic" ||
                !string.IsNullOrEmpty(pipelineEvent.ParsedData.IconPath))
            {
                return;
            }

            string iconPath = await _eventIconResolutionService.ResolveAsync(pipelineEvent.ParsedData);
            pipelineEvent.ParsedData.IconPath = string.IsNullOrEmpty(iconPath) ? "MISSING" : iconPath;
            pipelineEvent.Status = string.IsNullOrEmpty(iconPath) ? "Missing Icon" : "Ready";
        }

        private async Task ResolvePendingIconsAsync(CancellationToken cancellationToken)
        {
            var pendingEvents = _model.ProcessedEvents
                .Where(pipelineEvent => pipelineEvent.ParsedData != null &&
                                        pipelineEvent.ParsedData.IconType != "generic" &&
                                        string.IsNullOrEmpty(pipelineEvent.ParsedData.IconPath))
                .ToList();
            if (pendingEvents.Count == 0) return;

            int total = pendingEvents.Count;
            int resolved = 0;
            _progressService.SetStatus($"Resolving icons: 0/{total} (0%)");

            using var semaphore = new SemaphoreSlim(3, 3);
            Task[] resolutionTasks = pendingEvents.Select(async pipelineEvent =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string iconPath = await _eventIconResolutionService.ResolveAsync(pipelineEvent.ParsedData);
                    if (string.IsNullOrEmpty(iconPath))
                    {
                        _logger.LogWarn($"Failed to resolve icon for event '{pipelineEvent.FolderName}' ({pipelineEvent.ParsedData.IconType}:{pipelineEvent.ParsedData.IconLookupName}). The event will remain without an icon.");
                    }

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        pipelineEvent.ParsedData.IconPath = iconPath;
                        pipelineEvent.Status = string.IsNullOrEmpty(iconPath) ? "Missing Icon" : "Ready";
                    });
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Analysis was superseded by a newer user action.
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Background icon resolution failed for {pipelineEvent.FolderName}", ex);
                }
                finally
                {
                    int currentResolved = Interlocked.Increment(ref resolved);
                    double progressValue = (double)currentResolved / total * 100.0;
                    _progressService.SetStatus($"Resolving icons: {currentResolved}/{total} ({progressValue:0}%)");
                    semaphore.Release();
                }
            }).ToArray();

            try
            {
                await Task.WhenAll(resolutionTasks);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Hydration is best-effort and is safely interrupted by new analysis.
            }
        }
    }
}
