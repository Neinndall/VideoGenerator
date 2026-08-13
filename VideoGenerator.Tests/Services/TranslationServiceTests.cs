using System.Text.Json;
using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests;

public sealed class TranslationServiceTests
{
    [Fact]
    public void FormatsNamedAndIndexedPlaceholdersFromLocalTranslations()
    {
        string root = CreateRoot();
        string path = Path.Combine(root, "translations.json");

        try
        {
            File.WriteAllText(path, """
                {
                  "EN": {
                    "named": "Hello {name}",
                    "indexed": "Welcome {0}"
                  }
                }
                """);

            var service = new TranslationService(new LogService(), path);

            Assert.Equal("Hello Daniel", service.GetText(
                "EN",
                "named",
                new Dictionary<string, string> { ["name"] = "Daniel" }));
            Assert.Equal("Welcome Daniel", service.GetText("EN", "indexed", "Daniel"));
            Assert.True(service.KeyExists("named"));
            Assert.Contains("EN", service.AvailableLanguages);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void UpdatesTranslationAndPersistsItForTheNextServiceInstance()
    {
        string root = CreateRoot();
        string path = Path.Combine(root, "translations.json");

        try
        {
            File.WriteAllText(path, "{ \"EN\": {} }");
            var service = new TranslationService(new LogService(), path);

            service.UpdateTranslation("EN", "custom_key", "Updated value");

            var reloaded = new TranslationService(new LogService(), path);
            Assert.Equal("Updated value", reloaded.GetText("EN", "custom_key"));
            Assert.Equal("Updated value", JsonDocument.Parse(File.ReadAllText(path))
                .RootElement.GetProperty("EN")
                .GetProperty("custom_key")
                .GetString());
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void SaveRawJsonReplacesTheLocalTranslationDocument()
    {
        string root = CreateRoot();
        string path = Path.Combine(root, "translations.json");

        try
        {
            File.WriteAllText(path, "{ \"EN\": { \"old\": \"Old\" } }");
            var service = new TranslationService(new LogService(), path);

            service.SaveRawJson("{ \"ES\": { \"new\": \"Nuevo\" } }");

            Assert.Equal("Nuevo", service.GetText("ES", "new"));
            Assert.Equal("Nuevo", JsonDocument.Parse(service.GetRawJson())
                .RootElement.GetProperty("ES")
                .GetProperty("new")
                .GetString());
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static string CreateRoot() =>
        Directory.CreateTempSubdirectory("VideoGenerator.Translations.").FullName;

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
