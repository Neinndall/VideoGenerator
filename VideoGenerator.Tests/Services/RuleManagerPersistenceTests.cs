using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using Xunit;

namespace VideoGenerator.Tests.Services;

public sealed class RuleManagerPersistenceTests
{
    [Fact]
    public void OfficialRulesAreAddedWithoutOverwritingUserIconOverrides()
    {
        string root = Directory.CreateTempSubdirectory("VG.MappingPersistence.").FullName;
        try
        {
            string path = Path.Combine(root, "event_rules.json");
            var originals = new[]
            {
                new EventRule
                {
                    Keyword = "FirstEncounter",
                    TranslationKey = "my_custom_encounter",
                    Section = "MY_EVENTS",
                    IconType = "skinline",
                    IconLookup = "Arcade",
                    Type = RuleType.Interaction,
                    ExtractsTarget = true
                },
                new EventRule
                {
                    Keyword = "UnreleasedEvent",
                    TranslationKey = "unreleased_event",
                    IconType = "monster",
                    IconLookup = "Dinosaurio",
                    Type = RuleType.Target
                }
            };
            File.WriteAllText(path, JsonSerializer.Serialize(originals));

            var first = new RuleManager(new LogService(), path);
            var second = new RuleManager(new LogService(), path);
            var rule = Assert.Single(second.Rules, item => item.Keyword == "FirstEncounter");
            Assert.Equal("skinline", rule.IconType);
            Assert.Equal("Arcade", rule.IconLookup);
            Assert.Equal("my_custom_encounter", rule.TranslationKey);
            Assert.Equal("MY_EVENTS", rule.Section);
            Assert.Equal(RuleType.Interaction, rule.Type);
            Assert.True(rule.ExtractsTarget);
            Assert.Contains(second.Rules, item => item.Keyword == "UnreleasedEvent");
            Assert.Contains(second.Rules, item => item.Keyword == "Kill");
            Assert.Contains(second.Rules, item => item.Keyword == "ReceiveDamage");
            Assert.Contains(second.Rules, item => item.Keyword == "ReceiveHealShield");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void InvalidSavedFileIsNotDestroyedDuringFallbackToOfficialRules()
    {
        string root = Directory.CreateTempSubdirectory("VG.InvalidMapping.").FullName;
        try
        {
            string file = Path.Combine(root, "event_rules.json");
            const string original = "{ malformed, still belongs to the user";
            File.WriteAllText(file, original);
            var manager = new RuleManager(new LogService(), file);
            Assert.Contains(manager.Rules, rule => rule.Keyword == "FirstEncounter");
            Assert.Equal(original, File.ReadAllText(file));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
