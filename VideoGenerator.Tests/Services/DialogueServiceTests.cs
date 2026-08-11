using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests;

public sealed class DialogueServiceTests
{
    [Fact]
    public void CleanDialogueRemovesWhisperTagsAndNormalizesWhitespace()
    {
        string cleaned = DialogueService.CleanDialogue("[MUSIC]  Hello   brave [BLANK_AUDIO] world");

        Assert.Equal("Hello brave world", cleaned);
    }

    [Fact]
    public void CleanDialoguePreservesParenthesizedDialogue()
    {
        string cleaned = DialogueService.CleanDialogue("Hello (laughs) [MUSIC]");

        Assert.Equal("Hello (laughs)", cleaned);
    }
}
