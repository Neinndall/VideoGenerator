using FFMpegCore;
using System.Diagnostics;
using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests;

public sealed class TimelineWaveformServiceTests
{
    [Fact]
    public async Task RealWaveformDistinguishesSilentAudioFromRecordedTone()
    {
        string dir = Directory.CreateTempSubdirectory("VG.Waveform.").FullName;
        try
        {
            var service = new VideoService(new LogService());
            await service.EnsureBinariesReadyAsync();
            string ffmpeg = Path.Combine(GlobalFFOptions.Current.BinaryFolder, "ffmpeg.exe");
            string silence = Path.Combine(dir, "silent audio.wav");
            string tone = Path.Combine(dir, "test tone.wav");
            await MakeAudioAsync(ffmpeg, silence,
                "anullsrc=r=4000:cl=mono:d=0.8");
            await MakeAudioAsync(ffmpeg, tone,
                "sine=frequency=440:sample_rate=4000:duration=0.8");

            var waves = new TimelineWaveformService(service);
            var quiet = await waves.GetPeaksAsync(silence, 0, 0.7, 32);
            var audible = await waves.GetPeaksAsync(tone, 0, 0.7, 32);

            Assert.Equal(32, quiet.Count);
            Assert.All(quiet, value => Assert.InRange(value, 2, 2.01));
            Assert.Equal(32, audible.Count);
            Assert.Contains(audible, value => value > 8);
            Assert.All(audible, value => Assert.InRange(value, 2, 32));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    private static async Task MakeAudioAsync(string ffmpeg, string path, string lavfi)
    {
        var start = new ProcessStartInfo(ffmpeg)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true
        };
        foreach (var arg in new[]
        {
            "-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", lavfi,
            "-c:a", "pcm_s16le", "-y", path
        })
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)
            ?? throw new IOException("Unable to generate audio test fixture.");
        Task<string> errors = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, $"FFmpeg fixture: {await errors}");
    }
}
