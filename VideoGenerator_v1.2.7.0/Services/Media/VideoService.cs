using FFMpegCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using System.Security.Cryptography;
using System.Text;
using VideoGenerator.Models;
using VideoGenerator.Utils;

namespace VideoGenerator.Services
{
    public class VideoService
    {
        private readonly LogService _logger;
        private readonly string _ffmpegDir;
        private readonly string _cacheDir;
        private readonly SemaphoreSlim _binaryInitializationGate = new(1, 1);
        private bool _binariesReady;

        public VideoService(LogService logger, string cacheDirectory = null)
        {
            _logger = logger;
            _ffmpegDir = Path.Combine(Path.GetTempPath(), "VideoGenerator_FFmpeg");
            _cacheDir = string.IsNullOrWhiteSpace(cacheDirectory) ? AppConfig.CacheDir : cacheDirectory;
        }

        public async Task EnsureBinariesReadyAsync()
        {
            if (_binariesReady && ValidateFFmpeg()) return;

            await _binaryInitializationGate.WaitAsync();
            try
            {
                if (_binariesReady && ValidateFFmpeg()) return;

                if (ValidateFFmpeg())
                {
                    _binariesReady = true;
                    return;
                }

                _logger.LogInfo("FFmpeg binaries not found. Extracting to temp folder...");
                DirectoriesCreator.CreateDirectory(_ffmpegDir);
                
                var assembly = Assembly.GetExecutingAssembly();
                string resourcePrefix = "VideoGenerator.Resources.ffmpeg.";
                
                var resources = assembly.GetManifestResourceNames()
                    .Where(r => r.StartsWith(resourcePrefix));

                foreach (var resourceName in resources)
                {
                    string fileName = resourceName.Substring(resourcePrefix.Length);
                    string targetPath = Path.Combine(_ffmpegDir, fileName);
                    
                    if (!File.Exists(targetPath) || new FileInfo(targetPath).Length == 0)
                    {
                        using Stream stream = assembly.GetManifestResourceStream(resourceName);
                        if (stream != null)
                        {
                            using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
                            await stream.CopyToAsync(fileStream);
                        }
                    }
                }

                // Configure GlobalFFOptions only after extraction
                GlobalFFOptions.Configure(options => {
                    options.BinaryFolder = _ffmpegDir;
                });

                if (ValidateFFmpeg())
                {
                    _binariesReady = true;
                    _logger.LogInfo("FFmpeg binaries extracted successfully.");
                }
                else
                {
                    throw new Exception("Extraction completed but binaries are still missing.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to extract FFmpeg binaries", ex);
                throw;
            }
            finally
            {
                _binaryInitializationGate.Release();
            }
        }

        public static int CalculateVideoWorkUnits(int imageCount, int audioCount, double silenceDuration)
        {
            if (imageCount > 1)
            {
                int silenceUnits = silenceDuration > 0 ? 1 : 0;
                int appendedSilenceUnits = silenceDuration > 0 ? Math.Max(0, audioCount - 1) : 0;
                return silenceUnits + appendedSilenceUnits + audioCount + 1;
            }

            if (audioCount > 1)
                return (silenceDuration > 0 ? 1 : 0) + 2;

            return 1;
        }

        public async Task<bool> CreateVideoAsync(
            string imagePath,
            List<string> audioPaths,
            string outputPath,
            double silenceDuration,
            string dialogue = "",
            Action<string> onWorkCompleted = null,
            CancellationToken cancellationToken = default)
        {
            if (audioPaths == null || audioPaths.Count == 0)
                throw new ArgumentException("At least one audio track is required.", nameof(audioPaths));

            return await CreateVideoAsync(new List<string> { imagePath }, audioPaths, outputPath, silenceDuration, dialogue, onWorkCompleted, cancellationToken);
        }

        public string GetMergedAudioFamilyPath(
            IReadOnlyList<string> audioPaths,
            string eventName,
            string familyName,
            string eventPath)
        {
            if (audioPaths == null || audioPaths.Count == 0)
                return null;

            string Sanitize(string value) => string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var fingerprint = new StringBuilder()
                .Append(eventPath)
                .Append('|')
                .Append(eventName)
                .Append('|')
                .Append(familyName);

            foreach (string audioPath in audioPaths)
            {
                try
                {
                    var fileInfo = new FileInfo(audioPath);
                    fingerprint
                        .Append('|')
                        .Append(Path.GetFullPath(audioPath))
                        .Append('|')
                        .Append(fileInfo.Exists ? fileInfo.Length : -1)
                        .Append('|')
                        .Append(fileInfo.Exists ? fileInfo.LastWriteTimeUtc.Ticks : 0);
                }
                catch
                {
                    fingerprint.Append('|').Append(audioPath);
                }
            }

            string sourceId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint.ToString())))[..16];
            string familyCacheDir = Path.Combine(_cacheDir, "AudioFamilies");
            string currentPath = Path.Combine(familyCacheDir, $"{Sanitize(eventName)}_{Sanitize(familyName)}_{sourceId}.wav");
            if (File.Exists(currentPath) && new FileInfo(currentPath).Length > 0)
                return currentPath;

            // Keep compatibility with merged families created before the source
            // fingerprint was added to the cache filename.
            string legacySourceId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(eventPath)))[..12];
            string legacyPath = Path.Combine(familyCacheDir, $"{Sanitize(eventName)}_{Sanitize(familyName)}_{legacySourceId}.wav");
            return IsCacheCurrent(legacyPath, audioPaths)
                ? legacyPath
                : currentPath;
        }

        private static bool IsCacheCurrent(string cachePath, IReadOnlyList<string> sourceAudioPaths)
        {
            try
            {
                if (!File.Exists(cachePath) || new FileInfo(cachePath).Length == 0)
                    return false;

                DateTime cacheWriteTime = File.GetLastWriteTimeUtc(cachePath);
                return sourceAudioPaths.All(audioPath =>
                    File.Exists(audioPath) && File.GetLastWriteTimeUtc(audioPath) <= cacheWriteTime);
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> MergeAudioFamilyAsync(
            IReadOnlyList<string> audioPaths,
            string eventName,
            string familyName,
            string eventPath,
            CancellationToken cancellationToken = default)
        {
            if (audioPaths == null || audioPaths.Count == 0)
                throw new ArgumentException("An audio family must contain at least one file.", nameof(audioPaths));

            string outputPath = GetMergedAudioFamilyPath(audioPaths, eventName, familyName, eventPath);
            if (!string.IsNullOrEmpty(outputPath) && File.Exists(outputPath) && new FileInfo(outputPath).Length > 0)
            {
                _logger.LogDebug($"Reusing cached audio family: {Path.GetFileName(outputPath)}");
                return outputPath;
            }

            await EnsureBinariesReadyAsync();
            cancellationToken.ThrowIfCancellationRequested();

            string familyCacheDir = Path.GetDirectoryName(outputPath) ?? Path.Combine(_cacheDir, "AudioFamilies");
            DirectoriesCreator.CreateDirectory(familyCacheDir);
            string concatPath = Path.Combine(familyCacheDir, $"concat_{Guid.NewGuid():N}.txt");

            try
            {
                using (var writer = new StreamWriter(concatPath))
                {
                    foreach (string audioPath in audioPaths)
                    {
                        string escapedPath = Path.GetFullPath(audioPath).Replace("\\", "/").Replace("'", "'\\''");
                        writer.WriteLine($"file '{escapedPath}'");
                    }
                }

                bool result = await FFMpegArguments
                    .FromFileInput(concatPath, true, options => options.WithCustomArgument("-f concat -safe 0"))
                    .OutputToFile(outputPath, true, options => options.WithAudioCodec("pcm_s16le"))
                    .CancellableThrough(cancellationToken)
                    .ProcessAsynchronously();

                if (!result || !File.Exists(outputPath))
                    throw new InvalidOperationException($"FFmpeg could not merge audio family '{familyName}'.");

                return outputPath;
            }
            finally
            {
                try { if (File.Exists(concatPath)) File.Delete(concatPath); } catch { }
            }
        }

        public async Task<bool> CreateVideoAsync(
            List<string> imagePaths,
            List<string> audioPaths,
            string outputPath,
            double silenceDuration,
            string dialogue = "",
            Action<string> onWorkCompleted = null,
            CancellationToken cancellationToken = default)
        {
            if (imagePaths == null || imagePaths.Count == 0)
                throw new ArgumentException("At least one image is required.", nameof(imagePaths));
            if (audioPaths == null || audioPaths.Count == 0)
                throw new ArgumentException("At least one audio track is required.", nameof(audioPaths));

            string tempAudioPath = null;
            string silentAudioPath = null;
            string concatListPath = null;
            string finalAudioInput = null;
            string srtPath = null;
            var temporaryMediaPaths = new List<string>();

            try
            {
                // Defer to lazy initialization
                await EnsureBinariesReadyAsync();

                DirectoriesCreator.CreateParentDirectory(outputPath);
                string cacheDir = _cacheDir;
                DirectoriesCreator.CreateDirectory(cacheDir);

                if (imagePaths != null && imagePaths.Count > 1)
                {
                    var tempClips = new List<string>();

                    // 1. Create silent audio if needed
                    if (silenceDuration > 0)
                    {
                        silentAudioPath = Path.Combine(cacheDir, $"silent_audio_{Guid.NewGuid():N}.wav");
                        await FFMpegArguments
                            .FromFileInput("anullsrc=r=48000:cl=stereo", false, options => options
                                .ForceFormat("lavfi"))
                            .OutputToFile(silentAudioPath, true, options => options
                                .WithAudioCodec("pcm_s16le")
                                .WithDuration(TimeSpan.FromSeconds(silenceDuration)))
                            .CancellableThrough(cancellationToken)
                            .ProcessAsynchronously();
                        onWorkCompleted?.Invoke("Created silence track");
                    }

                    for (int i = 0; i < audioPaths.Count; i++)
                    {
                        string imageForClip = i < imagePaths.Count ? imagePaths[i] : imagePaths.Last();
                        string audioForClip = audioPaths[i];
                        string clipAudioInput = audioForClip;
                        string tempClipAudioPath = null;
                        string tempConcatPath = null;

                        // If silenceDuration > 0 and this is not the last audio track, append silence to this clip's audio
                        if (silenceDuration > 0 && i < audioPaths.Count - 1 && silentAudioPath != null)
                        {
                            tempClipAudioPath = Path.Combine(cacheDir, $"temp_clip_audio_{i}_{Guid.NewGuid()}.wav");
                            tempConcatPath = Path.Combine(cacheDir, $"temp_clip_concat_{i}_{Guid.NewGuid()}.txt");
                            temporaryMediaPaths.Add(tempClipAudioPath);
                            temporaryMediaPaths.Add(tempConcatPath);

                            using (var writer = new StreamWriter(tempConcatPath))
                            {
                                writer.WriteLine($"file '{Path.GetFullPath(audioForClip).Replace("\\", "/")}'");
                                writer.WriteLine($"file '{Path.GetFullPath(silentAudioPath).Replace("\\", "/")}'");
                            }

                            await FFMpegArguments
                                .FromFileInput(tempConcatPath, true, options => options
                                    .WithCustomArgument("-f concat -safe 0"))
                                .OutputToFile(tempClipAudioPath, true, options => options
                                    .WithAudioCodec("pcm_s16le"))
                                .CancellableThrough(cancellationToken)
                                .ProcessAsynchronously();

                            clipAudioInput = tempClipAudioPath;
                            onWorkCompleted?.Invoke($"Appended silence to audio {i + 1}/{audioPaths.Count}");
                        }

                        // Generate the temporary video clip
                        string clipOutputPath = Path.Combine(cacheDir, $"temp_clip_{i}_{Guid.NewGuid()}.mp4");
                        temporaryMediaPaths.Add(clipOutputPath);
                        var audioAnalysis = await FFProbe.AnalyseAsync(clipAudioInput);
                        var duration = audioAnalysis.Duration;

                        string customArgs = "-tune stillimage -preset ultrafast -pix_fmt yuv420p -crf 28 -shortest";

                        await FFMpegArguments
                            .FromFileInput(imageForClip, true, options => options.Loop(1))
                            .AddFileInput(clipAudioInput)
                            .OutputToFile(clipOutputPath, true, options => options
                                .WithVideoCodec("libx264")
                                .WithAudioCodec("aac")
                                .WithAudioBitrate(192)
                                .WithDuration(duration)
                                .WithCustomArgument(customArgs))
                            .CancellableThrough(cancellationToken)
                            .ProcessAsynchronously();

                        tempClips.Add(clipOutputPath);
                        onWorkCompleted?.Invoke($"Created temporary clip {i + 1}/{audioPaths.Count}");

                        // Clean up temporary audio files for this clip
                        try
                        {
                            if (tempClipAudioPath != null && File.Exists(tempClipAudioPath)) File.Delete(tempClipAudioPath);
                            if (tempConcatPath != null && File.Exists(tempConcatPath)) File.Delete(tempConcatPath);
                        }
                        catch { }
                    }

                    // Concatenate all temporary clips into the final video
                    concatListPath = Path.Combine(cacheDir, $"final_concat_{Guid.NewGuid()}.txt");
                    using (var writer = new StreamWriter(concatListPath))
                    {
                        foreach (var clip in tempClips)
                        {
                            writer.WriteLine($"file '{Path.GetFullPath(clip).Replace("\\", "/")}'");
                        }
                    }

                    var finalResult = await FFMpegArguments
                        .FromFileInput(concatListPath, true, options => options
                            .WithCustomArgument("-f concat -safe 0"))
                        .OutputToFile(outputPath, true, options => options
                            .WithCustomArgument("-c copy"))
                        .CancellableThrough(cancellationToken)
                        .ProcessAsynchronously();
                    onWorkCompleted?.Invoke("Concatenated final video");

                    // Clean up temporary clips
                    foreach (var clip in tempClips)
                    {
                        try { if (File.Exists(clip)) File.Delete(clip); } catch { }
                    }

                    if (finalResult)
                    {
                        _logger.LogInfo($"    [SUCCESS] Multi-image Video generated: {Path.GetFileName(outputPath)}");
                    }

                    return finalResult;
                }
                else
                {
                    // Fallback to single image logic
                    string singleImagePath = (imagePaths != null && imagePaths.Count > 0) ? imagePaths[0] : "";

                    if (audioPaths.Count > 1)
                    {
                        // 1. Create silent audio if needed
                        if (silenceDuration > 0)
                        {
                            silentAudioPath = Path.Combine(cacheDir, $"silent_audio_{Guid.NewGuid():N}.wav");
                            await FFMpegArguments
                                .FromFileInput("anullsrc=r=48000:cl=stereo", false, options => options
                                    .ForceFormat("lavfi"))
                                .OutputToFile(silentAudioPath, true, options => options
                                    .WithAudioCodec("pcm_s16le")
                                    .WithDuration(TimeSpan.FromSeconds(silenceDuration)))
                                .CancellableThrough(cancellationToken)
                                .ProcessAsynchronously();
                            onWorkCompleted?.Invoke("Created silence track");
                        }

                        // 2. Create Concat List
                        concatListPath = Path.Combine(cacheDir, $"concat_list_{Guid.NewGuid():N}.txt");
                        using (var writer = new StreamWriter(concatListPath))
                        {
                            for (int i = 0; i < audioPaths.Count; i++)
                            {
                                string path = Path.GetFullPath(audioPaths[i]).Replace("\\", "/");
                                writer.WriteLine($"file '{path}'");
                                
                                if (silentAudioPath != null && i < audioPaths.Count - 1)
                                {
                                    string sPath = Path.GetFullPath(silentAudioPath).Replace("\\", "/");
                                    writer.WriteLine($"file '{sPath}'");
                                }
                            }
                        }

                        // 3. Join audios
                        tempAudioPath = Path.Combine(cacheDir, $"temp_combined_audio_{Guid.NewGuid():N}.wav");
                        await FFMpegArguments
                            .FromFileInput(concatListPath, true, options => options
                                .WithCustomArgument("-f concat -safe 0"))
                            .OutputToFile(tempAudioPath, true, options => options
                                .WithAudioCodec("pcm_s16le"))
                            .CancellableThrough(cancellationToken)
                            .ProcessAsynchronously();
                        onWorkCompleted?.Invoke("Combined event audio");

                        finalAudioInput = tempAudioPath;
                    }
                    else
                    {
                        finalAudioInput = audioPaths[0];
                    }

                    // 4. Analysis and Assembly
                    if (!File.Exists(finalAudioInput)) 
                    {
                        throw new FileNotFoundException($"Audio target not found: {finalAudioInput}");
                    }

                    var audioAnalysis = await FFProbe.AnalyseAsync(finalAudioInput);
                    var duration = audioAnalysis.Duration;

                    if (!File.Exists(singleImagePath)) throw new FileNotFoundException($"Image input not found: {singleImagePath}");

                    string customArgs = "-tune stillimage -preset ultrafast -pix_fmt yuv420p -crf 28 -shortest";

                    var result = await FFMpegArguments
                        .FromFileInput(singleImagePath, true, options => options.Loop(1))
                        .AddFileInput(finalAudioInput)
                        .OutputToFile(outputPath, true, options => options
                            .WithVideoCodec("libx264")
                            .WithAudioCodec("aac")
                            .WithAudioBitrate(192)
                            .WithDuration(duration)
                            .WithCustomArgument(customArgs))
                        .CancellableThrough(cancellationToken)
                        .ProcessAsynchronously();
                    onWorkCompleted?.Invoke("Encoded final video");

                    if (result)
                    {
                        _logger.LogInfo($"    [SUCCESS] Video generated: {Path.GetFileName(outputPath)}");
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"FFmpeg failed for {Path.GetFileName(outputPath)}", ex);
                throw;
            }
            finally
            {
                try
                {
                    if (tempAudioPath != null && File.Exists(tempAudioPath)) File.Delete(tempAudioPath);
                    if (silentAudioPath != null && File.Exists(silentAudioPath)) File.Delete(silentAudioPath);
                    if (concatListPath != null && File.Exists(concatListPath)) File.Delete(concatListPath);
                    if (srtPath != null && File.Exists(srtPath)) File.Delete(srtPath);
                    foreach (string temporaryMediaPath in temporaryMediaPaths)
                    {
                        try
                        {
                            if (File.Exists(temporaryMediaPath)) File.Delete(temporaryMediaPath);
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        private bool ValidateFFmpeg()
        {
            string binFolder = GlobalFFOptions.Current.BinaryFolder;
            if (string.IsNullOrEmpty(binFolder)) return false;

            string ffmpeg = Path.Combine(binFolder, "ffmpeg.exe");
            string ffprobe = Path.Combine(binFolder, "ffprobe.exe");

            return File.Exists(ffmpeg) && File.Exists(ffprobe);
        }
    }
}
