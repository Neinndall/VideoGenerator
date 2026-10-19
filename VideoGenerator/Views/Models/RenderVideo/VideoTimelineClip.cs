using System;
using System.Collections.Generic;

namespace VideoGenerator.Views.Models;

/// <summary>A source-video segment measured in seconds from the beginning of its source.</summary>
public sealed class VideoTimelineClip
{
    public string SourcePath { get; set; } = string.Empty;

    public double TrimStartSeconds { get; set; }

    /// <summary>Length of the segment, not its end time in the source.</summary>
    public double DurationSeconds { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(SourcePath))
            errors.Add("El clip necesita un archivo de origen.");
        if (!double.IsFinite(TrimStartSeconds) || TrimStartSeconds < 0)
            errors.Add("El inicio del recorte debe ser un número no negativo.");
        if (!double.IsFinite(DurationSeconds) || DurationSeconds <= 0)
            errors.Add("La duración del clip debe ser un número positivo.");
        if (double.IsFinite(TrimStartSeconds) && double.IsFinite(DurationSeconds) &&
            TrimStartSeconds + DurationSeconds > TimeSpan.MaxValue.TotalSeconds)
            errors.Add("La duración total del clip supera el máximo admitido.");
        return errors;
    }
}
