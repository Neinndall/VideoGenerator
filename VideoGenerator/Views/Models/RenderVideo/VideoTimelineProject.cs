#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace VideoGenerator.Views.Models;

/// <summary>Editable timeline whose clip order is its playback order.</summary>
public sealed class VideoTimelineProject
{
    private static readonly JsonSerializerOptions ProjectJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string Name { get; set; } = "Nuevo proyecto";

    public List<VideoTimelineClip> Clips { get; set; } = new();

    public string? MusicPath { get; set; }

    /// <summary>Background music gain: zero is silent, one is full original volume.</summary>
    public double MusicVolume { get; set; } = 0.25;

    /// <summary>Hide the timeline video track in export (render a black video frame, retain audio).</summary>
    public bool MuteVideo { get; set; }

    /// <summary>Mute the audio of the ordered media clips in preview and export.</summary>
    public bool MuteClipAudio { get; set; }

    /// <summary>Linear clip-audio gain used for both monitor and exported audio.</summary>
    public double ClipAudioVolume { get; set; } = 1.0;

    /// <summary>Omit the background music track from export without losing its file selection.</summary>
    public bool MuteMusic { get; set; }

    /// <summary>Path of the JSON project. Not serialized; relative media paths resolve from here.</summary>
    [JsonIgnore]
    public string? ProjectFilePath { get; private set; }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("El proyecto necesita un nombre.");
        if (!double.IsFinite(MusicVolume) || MusicVolume < 0 || MusicVolume > 1)
            errors.Add("El volumen de música debe estar entre 0 y 1.");
        if (!double.IsFinite(ClipAudioVolume) || ClipAudioVolume < 0 || ClipAudioVolume > 1)
            errors.Add("El volumen de audio de los clips debe estar entre 0 y 1.");
        if (Clips is null)
        {
            errors.Add("Falta la lista de clips.");
        }
        else
        {
            for (int i = 0; i < Clips.Count; i++)
            {
                if (Clips[i] is null)
                {
                    errors.Add($"El clip {i + 1} es nulo.");
                    continue;
                }

                errors.AddRange(Clips[i].Validate().Select(error => $"Clip {i + 1}: {error}"));
            }
        }

        return errors;
    }

    public string ResolveMediaPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (Path.IsPathFullyQualified(path))
            return Path.GetFullPath(path);

        string baseDirectory = string.IsNullOrEmpty(ProjectFilePath)
            ? Environment.CurrentDirectory
            : Path.GetDirectoryName(ProjectFilePath)!;
        return Path.GetFullPath(Path.Combine(baseDirectory, path));
    }

    /// <summary>Save a valid draft atomically, retaining any existing JSON on failure.</summary>
    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var errors = Validate();
        if (errors.Count > 0)
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));

        string destination = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(directory);
        string partialPath = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(partialPath, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, this, ProjectJsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(partialPath, destination, overwrite: true);
            ProjectFilePath = destination;
        }
        finally
        {
            try { if (File.Exists(partialPath)) File.Delete(partialPath); } catch (IOException) { }
        }
    }

    /// <summary>Malformed JSON and invalid projects raise errors instead of resetting user edits.</summary>
    public static async Task<VideoTimelineProject> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read,
            FileShare.Read, 81920, FileOptions.Asynchronous);
        VideoTimelineProject? project;
        try
        {
            project = await JsonSerializer.DeserializeAsync<VideoTimelineProject>(stream,
                ProjectJsonOptions, cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("El archivo no contiene un proyecto de timeline válido.", exception);
        }

        if (project is null)
            throw new InvalidDataException("El archivo del proyecto está vacío.");
        var errors = project.Validate();
        if (errors.Count > 0)
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        project.ProjectFilePath = fullPath;
        return project;
    }
}
