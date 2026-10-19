using VideoGenerator.Utils;
using Xunit;

namespace VideoGenerator.Tests.Services;

public sealed class AssemblyVersionTests
{
    [Theory]
    [InlineData("2.0.0.0", "v2.0.0")]
    [InlineData("2.0.0.1", "v2.0.0.1")]
    [InlineData("2.1.5.0", "v2.1.5")]
    [InlineData("2.1.5.10", "v2.1.5.10")]
    [InlineData("1.3.0.3", "v1.3.0.3")]
    public void ShowsFourthComponentOnlyForNonzeroRevision(string assemblyVersion, string expected)
    {
        Assert.Equal(expected, AssemblyVersion.FormatDisplayVersion(System.Version.Parse(assemblyVersion)));
    }
}
