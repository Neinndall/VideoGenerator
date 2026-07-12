using Microsoft.Extensions.DependencyInjection;
using VideoGenerator.Services;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

var services = new ServiceCollection();

// --- Core Services (Singletons) ---
services.AddSingleton(_ => AppSettings.Instance);
services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromMinutes(15) });
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
services.AddSingleton<ImageGenerator>();
services.AddSingleton<VideoService>();
services.AddSingleton<TranscriptionService>();
services.AddSingleton<DialogueService>();
services.AddSingleton<TaskCancellationService>();
services.AddSingleton<ProgressService>();
services.AddSingleton<EventFilterService>();

var provider = services.BuildServiceProvider();
var parser = provider.GetRequiredService<NameParser>();
var iconManager = provider.GetRequiredService<IconManager>();
var dataFetcher = provider.GetRequiredService<DataFetcher>();

Console.WriteLine("=== VIDEO GENERATOR EVENT DEBUGGER ===");
Console.WriteLine("Syncing local databases...");
try
{
    string version = await dataFetcher.GetLatestLolVersionAsync();
    var dbBuilder = provider.GetRequiredService<DatabaseBuilder>();
    await dbBuilder.InitializeDatabasesAsync(version);
    Console.WriteLine($"Databases synced to version {version}.\n");
}
catch (Exception ex)
{
    Console.WriteLine($"Database sync warning: {ex.Message}\n");
}

string? lolVersion = null;

while (true)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("Enter folder name to debug (or 'exit' to quit): ");
    Console.ResetColor();
    string? input = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(input)) continue;
    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

    try
    {
        var parsed = await parser.ParseFolderNameAsync(input, "EN");
        if (parsed == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Parsed result is NULL.");
            Console.ResetColor();
            continue;
        }

        Console.WriteLine("\n--- PARSED EVENT DETAILS ---");
        Console.WriteLine($"Original Folder : {input}");
        Console.WriteLine($"DisplayText     : {parsed.DisplayText}");
        Console.WriteLine($"IconType        : {parsed.IconType}");
        Console.WriteLine($"IconLookupName  : {parsed.IconLookupName}");

        if (parsed.IconType != "generic")
        {
            if (parsed.IconType is "champion" or "region" && lolVersion == null)
                lolVersion = await dataFetcher.GetLatestLolVersionAsync();

            Console.WriteLine("Resolving icon path...");
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

            if (string.IsNullOrEmpty(parsed.IconPath))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Resolved Icon   : MISSING / NOT FOUND");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Resolved Icon   : {parsed.IconPath}");
            }
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine("Resolved Icon   : Generic (No icon lookup needed)");
        }
        Console.WriteLine("----------------------------\n");
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error parsing event: {ex.Message}");
        Console.ResetColor();
    }
}
