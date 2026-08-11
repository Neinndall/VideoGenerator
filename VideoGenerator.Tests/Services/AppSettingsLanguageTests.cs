using Xunit;

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

}
