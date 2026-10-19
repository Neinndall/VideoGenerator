using System;
using System.Collections.Generic;

namespace VideoGenerator.Views.Models;

public sealed record TimelineExportPreset(string Name, int Width, int Height, int Fps, int Crf)
{
    public static IReadOnlyList<TimelineExportPreset> Presets { get; } = Array.AsReadOnly(new[]
    {
        new TimelineExportPreset("720p · Rápido", 1280, 720, 30, 25),
        new TimelineExportPreset("1080p · Equilibrado", 1920, 1080, 30, 21),
        new TimelineExportPreset("1080p · 60 FPS", 1920, 1080, 60, 20),
        new TimelineExportPreset("4K · Alta calidad", 3840, 2160, 30, 19)
    });

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("El preset necesita un nombre.");
        if (Width < 2 || Height < 2 || Width > 8192 || Height > 8192 || Width % 2 != 0 || Height % 2 != 0)
            errors.Add("La resolución debe ser par y estar entre 2 y 8192 píxeles.");
        if (Fps < 1 || Fps > 120)
            errors.Add("Los FPS deben estar entre 1 y 120.");
        if (Crf < 0 || Crf > 51)
            errors.Add("La calidad CRF debe estar entre 0 y 51.");
        return errors;
    }
}
