using Microsoft.Extensions.DependencyInjection;
using VideoGenerator.Services;
using System.IO;
using System.Net.Http;

var services = new ServiceCollection();
services.AddSingleton<HttpClient>();
services.AddSingleton<LogService>();
services.AddSingleton<DatabaseBuilder>();
services.AddSingleton<DataFetcher>();
services.AddSingleton<TranslationService>();
services.AddSingleton<RuleManager>();
services.AddSingleton<SkinlineManager>();
services.AddSingleton<GroupManager>();
services.AddSingleton<AliasManager>();
services.AddSingleton<IconManager>();
services.AddSingleton<NameParser>();

var provider = services.BuildServiceProvider();
var parser = provider.GetRequiredService<NameParser>();
var iconManager = provider.GetRequiredService<IconManager>();
var dataFetcher = provider.GetRequiredService<DataFetcher>();

Console.WriteLine("Syncing local databases (champions, items, monsters, structures)...");
try
{
    string version = await dataFetcher.GetLatestLolVersionAsync();
    var dbBuilder = provider.GetRequiredService<DatabaseBuilder>();
    await dbBuilder.InitializeDatabasesAsync(version);
    Console.WriteLine($"Databases synced to version {version}.");
}
catch (Exception ex)
{
    Console.WriteLine($"Database sync warning: {ex.Message}");
}

string root = args.Length > 0 ? args[0] : @"C:\Users\danielpriego\Downloads\Workspace\VideoGenerator\audios";
if (!Directory.Exists(root))
{
    Console.WriteLine($"Directory not found: {root}");
    return;
}

var dirs = Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
    .Concat(new[] { root })
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .OrderBy(d => d)
    .ToList();

int ok = 0, pending = 0, missingIcon = 0, errors = 0;
var pendingList = new List<string>();
var missingIconList = new List<string>();
var errorList = new List<string>();

string? lolVersion = null;

foreach (var dir in dirs)
{
    var audioFiles = Directory.GetFiles(dir).Where(f => f.EndsWith(".mp3") || f.EndsWith(".wav") || f.EndsWith(".ogg")).ToList();
    if (audioFiles.Count == 0) continue;

    string folderName = Path.GetFileName(dir);
    try
    {
        var parsed = await parser.ParseFolderNameAsync(folderName, "EN");
        if (parsed == null || string.IsNullOrEmpty(parsed.DisplayText) || parsed.DisplayText.Contains("event_") || parsed.DisplayText.Contains("interaction_") || parsed.DisplayText.Equals(folderName))
        {
            pending++;
            pendingList.Add(folderName);
            continue;
        }

        if (parsed.IconType != "generic")
        {
            if (parsed.IconType is "champion" or "region" && lolVersion == null)
                lolVersion = await dataFetcher.GetLatestLolVersionAsync();

            parsed.IconPath = parsed.IconType switch
            {
                "champion" => await iconManager.GetChampionIconAsync(parsed.IconLookupName, lolVersion),
                "item" => await iconManager.GetItemIconAsync(parsed.IconLookupName),
                "monster" => await iconManager.GetMonsterIconAsync(parsed.IconLookupName),
                "region" => await iconManager.GetChampionIconAsync(parsed.IconLookupName, lolVersion),
                "structure" => await iconManager.GetStructureIconAsync(parsed.IconLookupName),
                "system" => await iconManager.GetSystemIconAsync(parsed.IconLookupName),
                _ => null
            };
        }

        if (parsed.IconType != "generic" && string.IsNullOrEmpty(parsed.IconPath))
        {
            missingIcon++;
            missingIconList.Add($"{folderName} -> {parsed.DisplayText} ({parsed.IconType}:{parsed.IconLookupName})");
        }
        else
        {
            ok++;
        }
    }
    catch (Exception ex)
    {
        errors++;
        errorList.Add($"{folderName}: {ex.Message}");
    }
}

Console.WriteLine($"Total folders with audio: {dirs.Count}");
Console.WriteLine($"OK: {ok}");
Console.WriteLine($"Pending (untranslated/unparsed): {pending}");
Console.WriteLine($"Missing Icon: {missingIcon}");
Console.WriteLine($"Errors: {errors}");

Console.WriteLine("\n--- PENDING ---");
foreach (var item in pendingList.Take(30)) Console.WriteLine(item);
if (pendingList.Count > 30) Console.WriteLine($"... and {pendingList.Count - 30} more");

Console.WriteLine("\n--- MISSING ICON ---");
foreach (var item in missingIconList.Take(50)) Console.WriteLine(item);
if (missingIconList.Count > 50) Console.WriteLine($"... and {missingIconList.Count - 50} more");

Console.WriteLine("\n--- ERRORS ---");
foreach (var item in errorList.Take(30)) Console.WriteLine(item);
if (errorList.Count > 30) Console.WriteLine($"... and {errorList.Count - 30} more");
