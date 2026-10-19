using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using VideoGenerator.Services;
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
                if (EventIconResolutionService.RequiresIcon(pipelineEvent.ParsedData) &&
                    pipelineEvent.Status != EventStatuses.MissingIcon &&
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
            if (pipelineEvent == null || !EventIconResolutionService.RequiresIcon(pipelineEvent.ParsedData) ||
                pipelineEvent.Status == EventStatuses.MissingIcon ||
                !string.IsNullOrEmpty(pipelineEvent.ParsedData.IconPath))
            {
                return;
            }

            string iconPath = await _eventIconResolutionService.ResolveAsync(pipelineEvent.ParsedData);
            // Keep a failed resolution as null; the user can retry without rescanning.
            pipelineEvent.ParsedData.IconPath = iconPath;
            pipelineEvent.UpdateStatusAfterIconResolution(iconPath);
        }

        private async Task ResolvePendingIconsAsync(CancellationToken cancellationToken, bool retryOnlyFailed = false)
        {
            var pendingEvents = _model.ProcessedEvents
                .Where(pipelineEvent => EventIconResolutionService.RequiresIcon(pipelineEvent.ParsedData) &&
                                        !pipelineEvent.NeedsMapping &&
                                        (!retryOnlyFailed || pipelineEvent.Status == EventStatuses.MissingIcon) &&
                                        string.IsNullOrEmpty(pipelineEvent.ParsedData.IconPath))
                .ToList();
            if (pendingEvents.Count == 0) return;

            int total = pendingEvents.Count;
            int resolved = 0;
            _progressService.Start($"Resolving icons: 0/{total} (0%)", indeterminate: false);

            // Numerous audio folders can reference the SAME thematic skinline,
            // champion or system icon. Resolve that semantic icon once per scan,
            // then hydrate all matching events. An actual missing icon remains a
            // Missing Icon status for every affected event, but causes only one
            // actionable warning instead of hundreds of duplicate warnings.
            var batches = pendingEvents.GroupBy(pipelineEvent =>
                pipelineEvent.ParsedData.IconType.Trim() + ":" +
                pipelineEvent.ParsedData.IconLookupName.Trim(),
                StringComparer.OrdinalIgnoreCase).ToList();
            using var semaphore = new SemaphoreSlim(3, 3);
            Task[] resolutionTasks = batches.Select(async batch =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    PreviewEventModel representative = batch.First();
                    string iconPath = await _eventIconResolutionService.ResolveAsync(representative.ParsedData);
                    if (string.IsNullOrEmpty(iconPath))
                    {
                        string affected = batch.Count() == 1
                            ? $"event '{representative.FolderName}'"
                            : $"{batch.Count()} events (e.g. '{representative.FolderName}')";
                        _logger.LogWarn($"Failed to resolve local WAD icon for {affected} ({representative.ParsedData.IconType}:{representative.ParsedData.IconLookupName}). These events remain marked Missing Icon.");
                    }

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        foreach (PreviewEventModel pipelineEvent in batch)
                        {
                            pipelineEvent.ParsedData.IconPath = iconPath;
                            pipelineEvent.UpdateStatusAfterIconResolution(iconPath);
                        }
                    });
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Analysis was superseded by a newer user action.
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Background icon resolution failed for {batch.First().ParsedData.IconType}:{batch.First().ParsedData.IconLookupName}", ex);
                }
                finally
                {
                    int currentResolved = Interlocked.Add(ref resolved, batch.Count());
                    double progressValue = (double)currentResolved / total * 100.0;
                    _progressService.Report(progressValue, $"Resolving icons: {currentResolved}/{total} ({progressValue:0}%)");
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

            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
