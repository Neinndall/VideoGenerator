using FFMpegCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using VideoGenerator.Models;

namespace VideoGenerator.Services
{
    public class VideoService
    {
        private readonly LogService _logger;
        private readonly string _ffmpegDir;

        public VideoService(LogService logger)
        {
            _logger = logger;
            _ffmpegDir = Path.Combine(Path.GetTempPath(), "VideoGenerator_FFmpeg");
        }

        public async Task EnsureBinariesReadyAsync()
        {
            if (ValidateFFmpeg()) return;

            _logger.LogInfo("FFmpeg binaries not found. Extracting to temp folder...");
            
            try
            {
                Directory.CreateDirectory(_ffmpegDir);
                
                var assembly = Assembly.GetExecutingAssembly();
                string resourcePrefix = "VideoGenerator.Resources.ffmpeg.";
                
                var resources = assembly.GetManifestResourceNames()
                    .Where(r => r.StartsWith(resourcePrefix));

                foreach (var resourceName in resources)
                {
                    string fileName = resourceName.Substring(resourcePrefix.Length);
                    string targetPath = Path.Combine(_ffmpegDir, fileName);
                    
                    if (!File.Exists(targetPath))
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
        }

        public async Task<bool> CreateVideoAsync(string imagePath, List<string> audioPaths, string outputPath, double silenceDuration, string dialogue = "")
        {
            string tempAudioPath = null;
            string silentAudioPath = null;
            string concatListPath = null;
            string finalAudioInput = null;
            string srtPath = null;

            try
            {
                // Defer to lazy initialization
                await EnsureBinariesReadyAsync();

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache");
                Directory.CreateDirectory(cacheDir);

                if (audioPaths.Count > 1)
                {
                    // 1. Create silent audio if needed
                    if (silenceDuration > 0)
                    {
                        silentAudioPath = Path.Combine(cacheDir, "silent_audio.wav");
                        await FFMpegArguments
                            .FromFileInput("anullsrc=r=48000:cl=stereo", false, options => options
                                .ForceFormat("lavfi"))
                            .OutputToFile(silentAudioPath, true, options => options
                                .WithAudioCodec("pcm_s16le")
                                .WithDuration(TimeSpan.FromSeconds(silenceDuration)))
                            .ProcessAsynchronously();
                    }

                    // 2. Create Concat List
                    concatListPath = Path.Combine(cacheDir, "concat_list.txt");
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
                    tempAudioPath = Path.Combine(cacheDir, "temp_combined_audio.wav");
                    await FFMpegArguments
                        .FromFileInput(concatListPath, true, options => options
                            .WithCustomArgument("-f concat -safe 0"))
                        .OutputToFile(tempAudioPath, true, options => options
                            .WithAudioCodec("pcm_s16le"))
                        .ProcessAsynchronously();

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

                if (!File.Exists(imagePath)) throw new FileNotFoundException($"Image input not found: {imagePath}");

                string customArgs = "-tune stillimage -preset ultrafast -pix_fmt yuv420p -crf 28 -shortest";

                var result = await FFMpegArguments
                    .FromFileInput(imagePath, true, options => options.Loop(1))
                    .AddFileInput(finalAudioInput)
                    .OutputToFile(outputPath, true, options => options
                        .WithVideoCodec("libx264")
                        .WithAudioCodec("aac")
                        .WithAudioBitrate(192)
                        .WithDuration(duration)
                        .WithCustomArgument(customArgs))
                    .ProcessAsynchronously();

                if (result)
                {
                    _logger.LogInfo($"    [SUCCESS] Video generated: {Path.GetFileName(outputPath)}");
                }

                return result;
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
