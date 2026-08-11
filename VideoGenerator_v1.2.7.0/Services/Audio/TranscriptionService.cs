using FFMpegCore;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using Whisper.net;
using VideoGenerator.Models;
using VideoGenerator.Utils;

namespace VideoGenerator.Services
{
    public class TranscriptionService
    {
        private readonly LogService _logger;
        private readonly VideoService _videoService;
        private readonly HttpClient _httpClient;
        private readonly AppSettings _settings;
        private readonly SemaphoreSlim _modelDownloadGate = new(1, 1);
        private WhisperFactory _whisperFactory;
        private string _loadedModelPath;
        private readonly object _modelLock = new object();

        private string GetModelFileName()
        {
            string model = _settings.WhisperModel?.ToLower() ?? "base";
            return $"ggml-{model}.bin";
        }

        private string GetModelPath()
        {
            return AppConfig.Paths.GetWhisperModelPath(GetModelFileName());
        }

        private string GetModelUrl()
        {
            return $"https://huggingface.co/ggerganov/whisper.cpp/resolve/main/{GetModelFileName()}";
        }

        public TranscriptionService(LogService logger, VideoService videoService, HttpClient httpClient, AppSettings settings)
        {
            _logger = logger;
            _videoService = videoService;
            _httpClient = httpClient;
            _settings = settings;
        }

        public async Task EnsureModelReadyAsync()
        {
            string modelPath = GetModelPath();
            string modelName = GetModelFileName();
            await _modelDownloadGate.WaitAsync();
            try
            {
                if (IsUsableModel(modelPath)) return;

                string modelUrl = GetModelUrl();
                _logger.LogInfo($"Whisper model ({modelName}) not found. Downloading from Hugging Face...");
                DirectoriesCreator.CreateParentDirectory(modelPath);

                using var response = await _httpClient.GetAsync(modelUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                string temporaryPath = $"{modelPath}.{Guid.NewGuid():N}.download";
                using var contentStream = await response.Content.ReadAsStreamAsync();
                try
                {
                    // Dispose the destination stream before moving the file. Windows
                    // refuses to move an open source file even when the destination
                    // overwrite flag is enabled.
                    using (var fileStream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        await contentStream.CopyToAsync(fileStream);
                        await fileStream.FlushAsync();
                    }

                    for (int attempt = 0; attempt < 5; attempt++)
                    {
                        if (IsUsableModel(modelPath))
                            break;

                        try
                        {
                            File.Move(temporaryPath, modelPath, true);
                            break;
                        }
                        catch (IOException) when (attempt < 4)
                        {
                            await Task.Delay(250 * (attempt + 1));
                        }
                    }

                    if (!IsUsableModel(modelPath))
                        throw new IOException($"Whisper model could not be moved into place: {modelPath}");
                }
                finally
                {
                    try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
                }

                _logger.LogInfo($"Whisper model ({modelName}) downloaded successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to download Whisper model file: {modelName}", ex);
                throw;
            }
            finally
            {
                _modelDownloadGate.Release();
            }
        }

        private static bool IsUsableModel(string modelPath)
        {
            try
            {
                return File.Exists(modelPath) && new FileInfo(modelPath).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> TranscribeAudioAsync(string audioFilePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(audioFilePath) || !File.Exists(audioFilePath))
            {
                return string.Empty;
            }

            await EnsureModelReadyAsync();
            await _videoService.EnsureBinariesReadyAsync();

            string tempWavPath = AppConfig.Paths.CreateTemporaryWavPath();
            
            try
            {
                // Whisper requires 16kHz, single-channel, 16-bit PCM WAV.
                // We use FFmpeg to convert any audio format (wav, mp3, ogg, wem) to this format.
                bool convertResult = await FFMpegArguments
                    .FromFileInput(audioFilePath)
                    .OutputToFile(tempWavPath, true, options => options
                        .WithAudioSamplingRate(16000)
                        .WithCustomArgument("-ac 1 -c:a pcm_s16le -af apad=whole_dur=3"))
                    .CancellableThrough(cancellationToken)
                    .ProcessAsynchronously();

                if (!convertResult || !File.Exists(tempWavPath))
                {
                    _logger.LogWarn("FFmpeg audio conversion failed for Whisper transcription.");
                    return string.Empty;
                }

                cancellationToken.ThrowIfCancellationRequested();

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

                string lang = _settings.WhisperLanguage ?? "auto";
                int threads = _settings.WhisperThreadCount;
                using var processor = factory.CreateBuilder()
                    .WithLanguage(lang)
                    .WithThreads(threads)
                    .Build();

                using var fileStream = new FileStream(tempWavPath, FileMode.Open, FileAccess.Read);
                
                var transcriptionText = "";
                await foreach (var segment in processor.ProcessAsync(fileStream, cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    transcriptionText += segment.Text + " ";
                }

                string cleanedResult = transcriptionText.Trim();
                
                if (_settings.CleanWhisperHallucinations)
                {
                    cleanedResult = DialogueService.CleanDialogue(cleanedResult);
                }

                _logger.LogDebug($"Transcription completed: {Path.GetFileName(audioFilePath)} | Characters: {cleanedResult.Length}");
                return cleanedResult;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarn("Transcription cancelled by user.");
                throw;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarn("Transcription interrupted by user cancellation.");
                    throw new OperationCanceledException(cancellationToken);
                }
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

        public async Task<string> TranscribeAudiosAsync(
            System.Collections.Generic.IEnumerable<string> audioFilePaths,
            Action<string> onAudioStart = null,
            Action<string> onAudioComplete = null,
            CancellationToken cancellationToken = default)
        {
            if (audioFilePaths == null) return string.Empty;

            var results = new System.Collections.Generic.List<string>();
            foreach (var path in audioFilePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                onAudioStart?.Invoke(path);
                string text = await TranscribeAudioAsync(path, cancellationToken);
                onAudioComplete?.Invoke(path);
                if (!string.IsNullOrEmpty(text))
                {
                    results.Add(text);
                }
            }
            return string.Join(" || ", results);
        }
    }
}
