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
        private WhisperFactory _whisperFactory;
        private string _loadedModelPath;
        private readonly object _modelLock = new object();

        private string GetModelFileName()
        {
            string model = AppSettings.Instance.WhisperModel?.ToLower() ?? "base";
            return $"ggml-{model}.bin";
        }

        private string GetModelPath()
        {
            return Path.Combine(AppConfig.CacheDir, GetModelFileName());
        }

        private string GetModelUrl()
        {
            return $"https://huggingface.co/ggerganov/whisper.cpp/resolve/main/{GetModelFileName()}";
        }

        public TranscriptionService(LogService logger, VideoService videoService)
        {
            _logger = logger;
            _videoService = videoService;
        }

        public async Task EnsureModelReadyAsync()
        {
            string modelPath = GetModelPath();
            if (File.Exists(modelPath)) return;

            string modelName = GetModelFileName();
            string modelUrl = GetModelUrl();
            _logger.LogInfo($"Whisper model ({modelName}) not found. Downloading from Hugging Face...");
            
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
                
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(15);
                
                using var response = await httpClient.GetAsync(modelUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(modelPath, FileMode.Create, FileAccess.Write, FileShare.None);
                
                await contentStream.CopyToAsync(fileStream);
                _logger.LogInfo($"Whisper model ({modelName}) downloaded successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to download Whisper model file: {modelName}", ex);
                if (File.Exists(modelPath)) File.Delete(modelPath);
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
                        .WithCustomArgument("-ac 1 -c:a pcm_s16le -af apad=whole_dur=3"))
                    .ProcessAsynchronously();

                if (!convertResult || !File.Exists(tempWavPath))
                {
                    _logger.LogWarn("FFmpeg audio conversion failed for Whisper transcription.");
                    return string.Empty;
                }

                string modelPath = GetModelPath();
                WhisperFactory factory;
                lock (_modelLock)
                {
                    if (_whisperFactory == null || _loadedModelPath != modelPath)
                    {
                        _logger.LogInfo($"Loading Whisper model into memory: {Path.GetFileName(modelPath)}...");
                        _whisperFactory?.Dispose();
                        _whisperFactory = WhisperFactory.FromPath(modelPath);
                        _loadedModelPath = modelPath;
                    }
                    factory = _whisperFactory;
                }

                string lang = AppSettings.Instance.WhisperLanguage ?? "auto";
                int threads = AppSettings.Instance.WhisperThreadCount;
                using var processor = factory.CreateBuilder()
                    .WithLanguage(lang)
                    .WithThreads(threads)
                    .Build();

                using var fileStream = new FileStream(tempWavPath, FileMode.Open, FileAccess.Read);
                
                var transcriptionText = "";
                await foreach (var segment in processor.ProcessAsync(fileStream))
                {
                    transcriptionText += segment.Text + " ";
                }

                string cleanedResult = transcriptionText.Trim();
                
                if (AppSettings.Instance.CleanWhisperHallucinations)
                {
                    cleanedResult = DialogueService.CleanDialogue(cleanedResult);
                }

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
            return string.Join(" || ", results);
        }
    }
}
