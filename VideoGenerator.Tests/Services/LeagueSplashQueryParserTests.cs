using System;
using System.Linq;
using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests.Services;

public sealed class LeagueSplashQueryParserTests
{
    [Theory]
    [InlineData("Aatrox", "Aatrox", "")]
    [InlineData("Aatrox_0", "Aatrox", "0")]
    [InlineData("Aatrox_01", "Aatrox", "1")]
    [InlineData("Aatrox1", "Aatrox", "1")]
    [InlineData("Aatrox 2", "Aatrox", "2")]
    [InlineData("AatroxSkin03", "Aatrox", "3")]
    [InlineData("Aatrox1,2", "Aatrox", "1,2")]
    [InlineData("Aatrox_1, 2,1", "Aatrox", "1,2")]
    [InlineData("Miss Fortune 14", "Miss Fortune", "14")]
    [InlineData("Kai'Sa", "Kai'Sa", "")]
    [InlineData("K'Sante_02", "K'Sante", "2")]
    public void AcceptsHumanFriendlyChampionSkinSyntax(string text, string champion, string ids)
    {
        Assert.True(LeagueSplashQueryParser.TryParse(text, out var parsed));
        Assert.Equal(champion, parsed.Champion);
        Assert.Equal(ids, string.Join(",", parsed.SkinIds));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Aatrox_splash_01")]
    [InlineData("assets/characters/aatrox")]
    [InlineData("Aatrox1,2,x")]
    [InlineData("Aatrox_1,99999")]
    public void RejectsPathsOrMalformedLists(string text)
    {
        Assert.False(LeagueSplashQueryParser.TryParse(text, out _));
    }
}
