using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VideoGenerator.Views.Models;

public sealed record TimelineExportPreset(
    string Name,
    int Width,
    int Height,
    int Fps,
    int Crf,
    string AudioBitrate = "192k",
    int AudioSampleRate = 48000,
    string Encoder = "libx264",
    string FfmpegPreset = "medium",
    bool IsBuiltIn = true,
    string Description = "")
{
    [JsonIgnore]
    public string ResolutionDisplay => $"{Width} × {Height}";

    [JsonIgnore]
    public string AspectRatioDisplay => (Width, Height) switch
    {
        (1920, 1080) => "16:9 (Horizontal)",
        (1280, 720) => "16:9 (Horizontal)",
        (2560, 1440) => "16:9 (2K QHD)",
        (3840, 2160) => "16:9 (4K UHD)",
        (1080, 1920) => "9:16 (Vertical · TikTok/Shorts)",
        (720, 1280) => "9:16 (Vertical)",
        (1080, 1080) => "1:1 (Cuadrado)",
        _ => $"{Width}:{Height}"
    };

    [JsonIgnore]
    public string SummaryDisplay =>
        $"{Width}×{Height} · {Fps} FPS · CRF {Crf} · {AudioBitrate} audio";

    public static IReadOnlyList<TimelineExportPreset> BuiltInPresets { get; } = Array.AsReadOnly(new[]
    {
        new TimelineExportPreset("YouTube 1080p · 60 FPS", 1920, 1080, 60, 20, "192k", 48000, "libx264", "medium", true, "Ideal para gameplay fluido y montajes en alta definición"),
        new TimelineExportPreset("YouTube 1080p · 30 FPS", 1920, 1080, 30, 21, "192k", 48000, "libx264", "medium", true, "Equilibrado estándar para vídeos de YouTube"),
        new TimelineExportPreset("TikTok / Shorts / Reels (9:16)", 1080, 1920, 60, 21, "192k", 48000, "libx264", "medium", true, "Formato vertical optimizado para redes sociales"),
        new TimelineExportPreset("1440p 2K QHD · 60 FPS", 2560, 1440, 60, 19, "256k", 48000, "libx264", "medium", true, "Alta fidelidad para monitores 1440p"),
        new TimelineExportPreset("4K Ultra HD · 30 FPS", 3840, 2160, 30, 19, "320k", 48000, "libx264", "medium", true, "Máxima resolución y calidad de exportación"),
        new TimelineExportPreset("720p · Rápido", 1280, 720, 30, 25, "128k", 48000, "libx264", "fast", true, "Renderizado rápido y archivos ligeros"),
        new TimelineExportPreset("Discord / Comprimido", 1280, 720, 30, 28, "128k", 48000, "libx264", "veryfast", true, "Archivos de tamaño reducido para compartir rápidamente")
    });

    public static IReadOnlyList<TimelineExportPreset> Presets => BuiltInPresets;

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
