using VideoGenerator.Services;
using VideoGenerator.Services.Parsers;
using VideoGenerator.Views.Models;
using Xunit;

namespace VideoGenerator.Tests.Integration;

public sealed class EventIconDeterminismTests
{
    [Fact]
    public async Task ThematicGroupUsesOneStableIconAcrossRepeatedAnalysisAndGroupReordering()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.IconDeterminism.").FullName;
        try
        {
            var logger = new LogService();
            var fetcher = new DataFetcher(logger, root);
            var aliases = new AliasManager(logger, Path.Combine(root, "aliases.json"));
            var groups = new GroupManager(logger, Path.Combine(root, "groups.json"));
            var thematicGroup = new ThematicGroup
            {
                Name = "Sentinels Test",
                Category = "Class",
                ChampionsRaw = "Lux, Ahri, Garen"
            };
            groups.Groups.Add(thematicGroup);

            var rules = new RuleManager(logger, Path.Combine(root, "rules.json"));
            rules.Rules.Add(new EventRule
            {
                Keyword = "ProbeOnly",
                Type = RuleType.Target,
                Section = "INTERACTIONS",
                TranslationKey = "interaction_first_encounter_one",
                IconType = "champion"
            });

            var translations = new TranslationService(logger, Path.Combine(root, "translations.json"));
            var skinlines = new SkinlineManager(fetcher, aliases, logger);
            var parser = new DynamicRuleParser(translations, fetcher, rules, groups, aliases, skinlines);
            const string eventName = "Play_vo_Aatrox_ProbeOnly3DSentinelsTest";

            var first = await parser.ParseAsync(eventName, "EN");
            Assert.NotNull(first);
            Assert.Equal("champion", first.IconType);
            Assert.Contains(first.IconLookupName, new[] { "Lux", "Ahri", "Garen" });

            for (int attempt = 0; attempt < 10; attempt++)
            {
                var repeat = await parser.ParseAsync(eventName, "EN");
                Assert.NotNull(repeat);
                Assert.Equal(first.IconLookupName, repeat.IconLookupName);
            }

            thematicGroup.ChampionsRaw = "Garen, Lux, Ahri";
            var reordered = await parser.ParseAsync(eventName, "EN");
            Assert.NotNull(reordered);
            Assert.Equal(first.IconLookupName, reordered.IconLookupName);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
