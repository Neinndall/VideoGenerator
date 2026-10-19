#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VideoGenerator.Models;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services;

/// <summary>
/// Manages factory built-in and user-customizable render templates,
/// persisted cleanly in %LOCALAPPDATA%/VideoGenerator/Config/render_presets.json.
/// </summary>
public sealed class RenderPresetManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly LogService? _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly List<TimelineExportPreset> _customPresets = new();
    private bool _initialized;

    public RenderPresetManager(LogService? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Loads user custom presets from disk and merges them with built-in presets.
    /// Safe for repeated invocations or isolated test environments.
    /// </summary>
    public async Task InitializeAsync(string? storageRoot = null, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _customPresets.Clear();
            string filePath = AppConfig.GetRenderPresetsFilePath(storageRoot);

            if (File.Exists(filePath))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
                    var loaded = JsonSerializer.Deserialize<List<TimelineExportPreset>>(json, JsonOptions);
                    if (loaded is not null)
                    {
                        foreach (var preset in loaded)
                        {
                            if (preset.Validate().Count == 0 &&
                                !TimelineExportPreset.BuiltInPresets.Any(b => string.Equals(b.Name, preset.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                // Ensure loaded user presets are marked as not built-in
                                _customPresets.Add(preset with { IsBuiltIn = false });
                            }
                        }
                    }
                    _logger?.LogInfo($"Loaded {_customPresets.Count} custom render presets from {filePath}");
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Could not deserialize custom render presets from {filePath}; preserving factory defaults.", ex);
                }
            }
            _initialized = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns all presets: factory built-in presets followed by user custom presets.
    /// </summary>
    public IReadOnlyList<TimelineExportPreset> GetAllPresets()
    {
        EnsureInitialized();
        var result = new List<TimelineExportPreset>(TimelineExportPreset.BuiltInPresets.Count + _customPresets.Count);
        result.AddRange(TimelineExportPreset.BuiltInPresets);
        lock (_customPresets)
        {
            result.AddRange(_customPresets);
        }
        return result;
    }

    /// <summary>
    /// Returns only user-created custom presets.
    /// </summary>
    public IReadOnlyList<TimelineExportPreset> GetCustomPresets()
    {
        EnsureInitialized();
        lock (_customPresets)
        {
            return _customPresets.ToList();
        }
    }

    /// <summary>
    /// Saves or updates a custom preset, atomically persisting to render_presets.json.
    /// </summary>
    public async Task<TimelineExportPreset> SavePresetAsync(
        TimelineExportPreset preset,
        string? storageRoot = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(preset);
        var errors = preset.Validate();
        if (errors.Count > 0)
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            string filePath = AppConfig.GetRenderPresetsFilePath(storageRoot);
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // A saved preset is always marked as custom
            var normalized = preset with { IsBuiltIn = false };

            int existingIndex = _customPresets.FindIndex(p => string.Equals(p.Name, normalized.Name, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                _customPresets[existingIndex] = normalized;
            }
            else
            {
                _customPresets.Add(normalized);
            }

            await SaveToDiskAtomicAsync(filePath, _customPresets, ct).ConfigureAwait(false);
            _logger?.LogInfo($"Saved custom render preset '{normalized.Name}' to {filePath}");
            return normalized;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Deletes a custom preset by name. Factory built-in presets cannot be deleted.
    /// </summary>
    public async Task<bool> DeleteCustomPresetAsync(
        string presetName,
        string? storageRoot = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(presetName))
            return false;

        // Built-ins cannot be deleted
        if (TimelineExportPreset.BuiltInPresets.Any(b => string.Equals(b.Name, presetName, StringComparison.OrdinalIgnoreCase)))
            return false;

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            int removed = _customPresets.RemoveAll(p => string.Equals(p.Name, presetName, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                string filePath = AppConfig.GetRenderPresetsFilePath(storageRoot);
                await SaveToDiskAtomicAsync(filePath, _customPresets, ct).ConfigureAwait(false);
                _logger?.LogInfo($"Removed custom render preset '{presetName}'");
                return true;
            }
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task SaveToDiskAtomicAsync(string destinationPath, List<TimelineExportPreset> presets, CancellationToken ct)
    {
        string? dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        string tempPath = Path.Combine(dir ?? Path.GetTempPath(), $"{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");
        string json = JsonSerializer.Serialize(presets, JsonOptions);
        await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);

        try
        {
            File.Move(tempPath, destinationPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best effort */ }
            }
            throw;
        }
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            // Sync fallback if called prior to async initialization
            try
            {
                InitializeAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger?.LogError("Synchronous preset initialization fallback failed; using built-ins.", ex);
            }
        }
    }
}
