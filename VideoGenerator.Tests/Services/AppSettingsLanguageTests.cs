using Xunit;

using VideoGenerator.Models;
using VideoGenerator.Services;

namespace VideoGenerator.Tests;

public sealed class AppSettingsLanguageTests
{
    [Fact]
    public void NewSettingsUseEnglishAsTheDefaultDictionaryLanguage()
    {
        var settings = new AppSettings();

        Assert.Equal("EN", settings.DefaultDictionaryLanguage);
    }

    [Theory]
    [InlineData("EN", "default")]
    [InlineData("ES", "es_es")]
    [InlineData("TR", "tr_tr")]
    [InlineData("ALL", "default")]
    [InlineData("all", "default")]
    [InlineData("", "default")]
    public void CdragonLocaleUsesSupportedProcessingLanguageMappings(string language, string expectedLocale)
    {
        Assert.Equal(expectedLocale, AppConfig.GetCdragonLocale(language));
    }

}
