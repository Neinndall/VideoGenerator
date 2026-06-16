using FFMpegCore;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Whisper.net;
using VideoGenerator.Models;

namespace VideoGenerator.Services
{
    public class TranscriptionService
    {
        private readonly LogService _logger;
        private readonly VideoService _videoService;
        private readonly string _modelPath;
        private const string ModelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin";

        public TranscriptionService(LogService logger, VideoService videoService)
        {
            _logger = logger;
            _videoService = videoService;
            _modelPath = Path.Combine(AppConfig.CacheDir, "ggml-tiny.bin");
        }

        public async Task EnsureModelReadyAsync()
        {
            if (File.Exists(_modelPath)) return;

            _logger.LogInfo("Whisper tiny model not found. Downloading from Hugging Face...");
            
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_modelPath)!);
                
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(10);
                
                using var response = await httpClient.GetAsync(ModelUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(_modelPath, FileMode.Create, FileAccess.Write, FileShare.None);
                
                await contentStream.CopyToAsync(fileStream);
                _logger.LogInfo("Whisper tiny model downloaded successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to download Whisper model file", ex);
                if (File.Exists(_modelPath)) File.Delete(_modelPath);
                throw;
            }
        }

        public async Task<string> TranscribeAudioAsync(string audioFilePath)
        {
            if (string.IsNullOrEmpty(audioFilePath) || !File.Exists(audioFilePath))
            {
                return string.Empty;
            }

            await EnsureModelReadyAsync();
            await _videoService.EnsureBinariesReadyAsync();

            string tempWavPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.wav");
            
            try
            {
                // Whisper requires 16kHz, single-channel, 16-bit PCM WAV.
                // We use FFmpeg to convert any audio format (wav, mp3, ogg, wem) to this format.
                _logger.LogInfo($"Converting audio to 16kHz mono WAV: {Path.GetFileName(audioFilePath)}");
                bool convertResult = await FFMpegArguments
                    .FromFileInput(audioFilePath)
                    .OutputToFile(tempWavPath, true, options => options
                        .WithAudioSamplingRate(16000)
                        .WithCustomArgument("-ac 1 -c:a pcm_s16le"))
                    .ProcessAsynchronously();

                if (!convertResult || !File.Exists(tempWavPath))
                {
                    _logger.LogWarn("FFmpeg audio conversion failed for Whisper transcription.");
                    return string.Empty;
                }

                _logger.LogInfo("Starting Whisper transcription...");
                using var whisperFactory = WhisperFactory.FromPath(_modelPath);
                using var processor = whisperFactory.CreateBuilder()
                    .WithLanguage("auto")
                    .Build();

                using var fileStream = new FileStream(tempWavPath, FileMode.Open, FileAccess.Read);
                
                var transcriptionText = "";
                await foreach (var segment in processor.ProcessAsync(fileStream))
                {
                    transcriptionText += segment.Text + " ";
                }

                string cleanedResult = transcriptionText.Trim();
                _logger.LogInfo($"Transcription result: \"{cleanedResult}\"");
                return cleanedResult;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during audio transcription", ex);
                return string.Empty;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempWavPath)) File.Delete(tempWavPath);
                }
                catch { }
            }
        }

        public async System.Threading.Tasks.Task<string> TranscribeAudiosAsync(System.Collections.Generic.IEnumerable<string> audioFilePaths)
        {
            if (audioFilePaths == null) return string.Empty;

            var results = new System.Collections.Generic.List<string>();
            foreach (var path in audioFilePaths)
            {
                string text = await TranscribeAudioAsync(path);
                if (!string.IsNullOrEmpty(text))
                {
                    results.Add(text);
                }
            }
            return string.Join(" ", results);
        }
    }
}
