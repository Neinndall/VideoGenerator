#nullable enable
using FFMpegCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services;

/// <summary>Renders an ordered timeline directly with the FFmpeg binaries managed by VideoService.</summary>
public sealed class VideoTimelineRenderService
{
    private static readonly CultureInfo NumberCulture = CultureInfo.InvariantCulture;
    private readonly VideoService _videoService;

    public VideoTimelineRenderService(VideoService videoService)
    {
        _videoService = videoService ?? throw new ArgumentNullException(nameof(videoService));
    }

    /// <summary>Checks the real video duration and audio availability without decoding the whole file.</summary>
    public async Task<TimelineMediaInfo> InspectClipAsync(string sourcePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        await _videoService.EnsureBinariesReadyAsync();
        ct.ThrowIfCancellationRequested();
        return await ProbeAsync(Path.GetFullPath(sourcePath), ct);
    }

    /// <summary>
    /// Generates H.264/AAC MP4, retaining the existing destination until the complete video
    /// has been written successfully. Progress values range from 0 to 1.
    /// </summary>
    public async Task ExportAsync(
        VideoTimelineProject project,
        string outputPath,
        TimelineExportPreset preset,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var errors = project.Validate().Concat(preset.Validate()).ToList();
        if (project.Clips is null || project.Clips.Count == 0)
            errors.Add("Añade al menos un clip a la línea de tiempo.");
        if (errors.Count != 0)
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));

        string destination = Path.GetFullPath(outputPath);
        if (!string.Equals(Path.GetExtension(destination), ".mp4", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("El archivo de salida debe tener extensión .mp4.", nameof(outputPath));

        var clips = project.Clips!.Select(clip => new RenderClip(
            project.ResolveMediaPath(clip.SourcePath), clip.TrimStartSeconds, clip.DurationSeconds)).ToList();

        string? musicPath = string.IsNullOrWhiteSpace(project.MusicPath) || project.MusicVolume == 0
            ? null
            : project.ResolveMediaPath(project.MusicPath);
        if (clips.Any(clip => PathsEqual(clip.Path, destination)) ||
            (musicPath is not null && PathsEqual(musicPath, destination)))
            throw new IOException("El vídeo exportado no puede sobrescribir un clip o archivo de música del proyecto.");

        ct.ThrowIfCancellationRequested();
        await _videoService.EnsureBinariesReadyAsync();
        ct.ThrowIfCancellationRequested();

        string ffmpeg = ResolveBinaryPath("ffmpeg.exe");
        var preparedClips = new List<PreparedClip>(clips.Count);
        foreach (RenderClip clip in clips)
        {
            ct.ThrowIfCancellationRequested();
            TimelineMediaInfo info = await ProbeAsync(clip.Path, ct);
            if (!info.HasVideo)
                throw new InvalidDataException($"El archivo no contiene vídeo: {clip.Path}");
            if (clip.TrimStart + clip.Duration > info.DurationSeconds + 0.025)
                throw new InvalidDataException($"El recorte excede la duración disponible: {clip.Path}");
            preparedClips.Add(new PreparedClip(clip, info.HasAudio));
        }

        if (musicPath is not null)
        {
            TimelineMediaInfo musicInfo = await ProbeAsync(musicPath, ct);
            if (!musicInfo.HasAudio)
                throw new InvalidDataException($"La música no contiene una pista de audio: {musicPath}");
        }

        double timelineDuration = clips.Sum(clip => clip.Duration);
        if (!double.IsFinite(timelineDuration) || timelineDuration <= 0 || timelineDuration > TimeSpan.MaxValue.TotalSeconds)
            throw new InvalidDataException("La duración total del montaje es inválida.");

        string directory = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(directory);
        string partialPath = Path.Combine(directory,
            $"{Path.GetFileName(destination)}.{Guid.NewGuid():N}.partial.mp4");

        try
        {
            var start = CreateStartInfo(ffmpeg);
            foreach (PreparedClip clip in preparedClips)
            {
                start.ArgumentList.Add("-i");
                start.ArgumentList.Add(clip.Clip.Path);
            }

            if (musicPath is not null)
            {
                // The music repeats for the duration of the composition, even when shorter.
                start.ArgumentList.Add("-stream_loop");
                start.ArgumentList.Add("-1");
                start.ArgumentList.Add("-i");
                start.ArgumentList.Add(musicPath);
            }

            start.ArgumentList.Add("-filter_complex");
            start.ArgumentList.Add(BuildFilterGraph(preparedClips, preset, musicPath is not null,
                project.MusicVolume, timelineDuration));
            string videoEncoder = string.Equals(preset.Encoder, "h264_nvenc", StringComparison.OrdinalIgnoreCase)
                ? "h264_nvenc"
                : "libx264";
            string ffmpegPreset = string.IsNullOrWhiteSpace(preset.FfmpegPreset) ? "medium" : preset.FfmpegPreset;
            string audioBitrate = string.IsNullOrWhiteSpace(preset.AudioBitrate) ? "192k" : preset.AudioBitrate;
            string audioSampleRate = preset.AudioSampleRate > 0 ? preset.AudioSampleRate.ToString(NumberCulture) : "48000";

            AddArgs(start, "-map", "[outv]", "-map", "[outa]", "-c:v", videoEncoder, "-preset", ffmpegPreset,
                "-crf", preset.Crf.ToString(NumberCulture), "-pix_fmt", "yuv420p", "-r",
                preset.Fps.ToString(NumberCulture), "-c:a", "aac", "-b:a", audioBitrate, "-ar", audioSampleRate,
                "-ac", "2", "-movflags", "+faststart", "-f", "mp4", partialPath);

            progress?.Report(0);
            await RunFfmpegAsync(start, timelineDuration, progress, ct);
            VideoService.PublishRenderedVideo(partialPath, destination, ct);
            progress?.Report(1);
        }
        finally
        {
            try { if (File.Exists(partialPath)) File.Delete(partialPath); } catch (IOException) { }
        }
    }

    internal static string BuildFilterGraph(
        IReadOnlyList<PreparedClip> clips, TimelineExportPreset preset,
        bool includeMusic, double musicVolume, double timelineDuration)
    {
        var graph = new StringBuilder();
        for (int i = 0; i < clips.Count; i++)
        {
            var item = clips[i];
            string start = F(item.Clip.TrimStart);
            string duration = F(item.Clip.Duration);
            // All video streams share resolution, frame rate, sample aspect and pixel format.
            // User paths never enter the filter graph, only controlled numeric values do.
            graph.Append($"[{i}:v:0]trim=start={start}:duration={duration},setpts=PTS-STARTPTS,")
                .Append($"fps={preset.Fps},scale={preset.Width}:{preset.Height}:")
                .Append("force_original_aspect_ratio=decrease:flags=lanczos,")
                .Append($"pad={preset.Width}:{preset.Height}:(ow-iw)/2:(oh-ih)/2,")
                .Append($"setsar=1,format=yuv420p[v{i}];");

            if (item.HasAudio)
            {
                graph.Append($"[{i}:a:0]atrim=start={start}:duration={duration},")
                    .Append("asetpts=PTS-STARTPTS,aresample=48000,")
                    .Append("aformat=sample_fmts=fltp:channel_layouts=stereo,")
                    .Append($"apad=whole_dur={duration},atrim=duration={duration}[a{i}];");
            }
            else
            {
                graph.Append("anullsrc=r=48000:cl=stereo,")
                    .Append($"atrim=duration={duration},asetpts=PTS-STARTPTS[a{i}];");
            }
        }

        for (int i = 0; i < clips.Count; i++)
            graph.Append($"[v{i}][a{i}]");
        graph.Append($"concat=n={clips.Count}:v=1:a=1[mergedv][mergeda];");

        if (includeMusic)
        {
            // Music input is last and loops at input level, so it always covers the timeline.
            graph.Append($"[{clips.Count}:a:0]aresample=48000,")
                .Append("aformat=sample_fmts=fltp:channel_layouts=stereo,")
                .Append($"volume={F(musicVolume)},atrim=duration={F(timelineDuration)},")
                .Append("asetpts=PTS-STARTPTS[background];")
                .Append("[mergeda][background]amix=inputs=2:duration=first:")
                .Append("dropout_transition=0:normalize=0,alimiter=limit=0.95[outa];");
        }
        else
        {
            graph.Append("[mergeda]anull[outa];");
        }

        graph.Append("[mergedv]null[outv]");
        return graph.ToString();
    }

    private async Task<TimelineMediaInfo> ProbeAsync(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("No se encuentra el archivo de vídeo o audio.", filePath);

        var start = CreateStartInfo(ResolveBinaryPath("ffprobe.exe"));
        AddArgs(start, "-v", "error", "-show_entries", "format=duration:stream=codec_type,duration",
            "-of", "json", filePath);

        using Process process = StartProcess(start);
        using var registration = ct.Register(() => TryKill(process));
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        string json = await stdout;
        string error = await stderr;
        ct.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
            throw new InvalidDataException($"FFprobe no puede abrir '{filePath}': {error.Trim()}");

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            bool hasVideo = false;
            bool hasAudio = false;
            double formatDuration = 0;
            double videoDuration = 0;
            double audioDuration = 0;

            if (root.TryGetProperty("format", out JsonElement format) &&
                format.TryGetProperty("duration", out JsonElement durationElement))
                formatDuration = ParseDuration(durationElement);

            if (root.TryGetProperty("streams", out JsonElement streams))
            {
                foreach (JsonElement stream in streams.EnumerateArray())
                {
                    if (!stream.TryGetProperty("codec_type", out JsonElement codec)) continue;
                    switch (codec.GetString())
                    {
                        case "video": hasVideo = true; break;
                        case "audio": hasAudio = true; break;
                    }

                    if (stream.TryGetProperty("duration", out JsonElement streamDuration))
                    {
                        double parsed = ParseDuration(streamDuration);
                        if (codec.GetString() == "video")
                            videoDuration = Math.Max(videoDuration, parsed);
                        else if (codec.GetString() == "audio")
                            audioDuration = Math.Max(audioDuration, parsed);
                    }
                }
            }

            // A longer audio track must never make a missing video segment appear valid.
            // Some containers omit per-stream durations; use the container as fallback.
            double duration = hasVideo && videoDuration > 0 ? videoDuration :
                !hasVideo && audioDuration > 0 ? audioDuration : formatDuration;
            if (!double.IsFinite(duration) || duration <= 0)
                throw new InvalidDataException($"No se pudo determinar la duración: {filePath}");
            return new TimelineMediaInfo(duration, hasVideo, hasAudio);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"FFprobe devolvió información inválida: {filePath}", exception);
        }
    }

    private static async Task RunFfmpegAsync(
        ProcessStartInfo start, double duration, IProgress<double>? progress, CancellationToken ct)
    {
        using Process process = StartProcess(start);
        using var registration = ct.Register(() => TryKill(process));
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        Task readProgress = ReadProgressAsync(process.StandardOutput, duration, progress);
        await Task.WhenAll(readProgress, process.WaitForExitAsync());
        string error = await stderr;
        ct.ThrowIfCancellationRequested();
        if (process.ExitCode != 0)
            throw new IOException($"No se pudo renderizar el vídeo con FFmpeg (código {process.ExitCode}): {error.Trim()}");
    }

    private static async Task ReadProgressAsync(StreamReader output, double duration, IProgress<double>? progress)
    {
        string? line;
        while ((line = await output.ReadLineAsync()) is not null)
        {
            double elapsed;
            if (line.StartsWith("out_time_us=", StringComparison.Ordinal) &&
                double.TryParse(line.AsSpan("out_time_us=".Length), NumberStyles.Float, NumberCulture, out double us))
                elapsed = us / 1_000_000;
            else if (line.StartsWith("out_time_ms=", StringComparison.Ordinal) &&
                double.TryParse(line.AsSpan("out_time_ms=".Length), NumberStyles.Float, NumberCulture, out double ms))
                elapsed = ms / 1_000_000; // FFmpeg labels microseconds as 'ms'.
            else if (line.StartsWith("out_time=", StringComparison.Ordinal) &&
                TimeSpan.TryParse(line["out_time=".Length..], NumberCulture, out TimeSpan time))
                elapsed = time.TotalSeconds;
            else
                continue;

            if (double.IsFinite(elapsed))
                progress?.Report(Math.Clamp(elapsed / duration, 0, 0.99));
        }
    }

    private static ProcessStartInfo CreateStartInfo(string binary)
    {
        var start = new ProcessStartInfo(binary)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        AddArgs(start, "-hide_banner");
        if (Path.GetFileName(binary).Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
            AddArgs(start, "-nostdin", "-y", "-loglevel", "error", "-nostats", "-stats_period", "0.25",
                "-progress", "pipe:1");
        return start;
    }

    private static void AddArgs(ProcessStartInfo start, params string[] arguments)
    {
        foreach (string argument in arguments)
            start.ArgumentList.Add(argument);
    }

    private static Process StartProcess(ProcessStartInfo start)
    {
        var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start())
                throw new IOException($"No se pudo iniciar {Path.GetFileName(start.FileName)}.");
            return process;
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    private static double ParseDuration(JsonElement duration)
    {
        if (duration.ValueKind == JsonValueKind.String &&
            double.TryParse(duration.GetString(), NumberStyles.Float, NumberCulture, out double value))
            return value;
        if (duration.ValueKind == JsonValueKind.Number && duration.TryGetDouble(out double number))
            return number;
        return 0;
    }

    private static string ResolveBinaryPath(string binary)
    {
        string folder = GlobalFFOptions.Current.BinaryFolder;
        if (string.IsNullOrWhiteSpace(folder))
            throw new FileNotFoundException("FFmpeg todavía no está preparado.", binary);
        string path = Path.Combine(folder, binary);
        if (!File.Exists(path))
            throw new FileNotFoundException("No se encuentra el ejecutable de FFmpeg.", path);
        return path;
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private static string F(double number) => number.ToString("0.#########", NumberCulture);

    internal sealed record RenderClip(string Path, double TrimStart, double Duration);
    internal sealed record PreparedClip(RenderClip Clip, bool HasAudio);
}

public sealed record TimelineMediaInfo(double DurationSeconds, bool HasVideo, bool HasAudio);
