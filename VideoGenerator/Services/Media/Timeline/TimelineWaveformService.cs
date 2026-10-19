#nullable enable
using FFMpegCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VideoGenerator.Services;

/// <summary>
/// Reads real audio amplitudes for the timeline. This intentionally does not
/// synthesize waveforms from file names, which would misrepresent silent clips.
/// </summary>
public sealed class TimelineWaveformService
{
    private const int SampleRate = 4000;
    private readonly VideoService _videoService;

    public TimelineWaveformService(VideoService videoService)
    {
        _videoService = videoService ?? throw new ArgumentNullException(nameof(videoService));
    }

    /// <summary>Returns actual per-bucket PCM peak heights (2..32 WPF pixels).</summary>
    public async Task<IReadOnlyList<double>> GetPeaksAsync(
        string path, double trimStartSeconds, double durationSeconds,
        int peakCount = 64, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!double.IsFinite(trimStartSeconds) || trimStartSeconds < 0 ||
            !double.IsFinite(durationSeconds) || durationSeconds <= 0 ||
            peakCount is < 1 or > 512)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Invalid waveform range or peak count.");

        await _videoService.EnsureBinariesReadyAsync();
        ct.ThrowIfCancellationRequested();
        string mediaPath = Path.GetFullPath(path);
        if (!File.Exists(mediaPath))
            throw new FileNotFoundException("Audio source is missing.", mediaPath);

        var start = new ProcessStartInfo(
            Path.Combine(GlobalFFOptions.Current.BinaryFolder, "ffmpeg.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string arg in new[]
        {
            "-hide_banner", "-loglevel", "error", "-nostdin",
            "-ss", trimStartSeconds.ToString("0.######", CultureInfo.InvariantCulture),
            "-i", mediaPath,
            "-t", durationSeconds.ToString("0.######", CultureInfo.InvariantCulture),
            "-map", "0:a:0", "-vn", "-ac", "1", "-ar", SampleRate.ToString(CultureInfo.InvariantCulture),
            "-f", "s16le", "pipe:1"
        })
            start.ArgumentList.Add(arg);

        using var process = Process.Start(start)
            ?? throw new IOException("Could not start FFmpeg audio waveform analysis.");
        using var registration = ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        });
        Task<string> stderr = process.StandardError.ReadToEndAsync(ct);
        var maxPeaks = new int[peakCount];
        var buffer = new byte[8192];
        long sampleIndex = 0;
        long expectedSamples = Math.Max(1L, (long)Math.Ceiling(durationSeconds * SampleRate));
        int priorByte = -1;

        while (true)
        {
            int length = await process.StandardOutput.BaseStream.ReadAsync(buffer, ct);
            if (length == 0) break;
            for (int i = 0; i < length; i++)
            {
                if (priorByte < 0)
                {
                    priorByte = buffer[i];
                    continue;
                }
                short sample = unchecked((short)(priorByte | (buffer[i] << 8)));
                priorByte = -1;
                int bucket = (int)Math.Min(peakCount - 1, sampleIndex * peakCount / expectedSamples);
                int magnitude = Math.Abs((int)sample);
                if (magnitude > maxPeaks[bucket])
                    maxPeaks[bucket] = magnitude;
                sampleIndex++;
            }
        }
        await process.WaitForExitAsync(ct);
        string errorText = await stderr;
        ct.ThrowIfCancellationRequested();
        if (process.ExitCode != 0)
            throw new InvalidDataException($"FFmpeg could not read waveform: {errorText.Trim()}");

        var heights = new double[peakCount];
        for (int i = 0; i < peakCount; i++)
        {
            double amplitude = maxPeaks[i] / 32768.0;
            heights[i] = Math.Clamp(2.0 + 30.0 * Math.Sqrt(amplitude), 2.0, 32.0);
        }
        return heights;
    }
}
