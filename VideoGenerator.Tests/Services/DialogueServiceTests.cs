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

    [Fact]
    public void PersistsAndRetrievesDialogueValidation()
    {
        string tempDir = System.IO.Directory.CreateTempSubdirectory("VideoGenerator.DialogueServiceTests.").FullName;
        try
        {
            string dialoguesPath = System.IO.Path.Combine(tempDir, "dialogues.json");
            string validationsPath = System.IO.Path.Combine(tempDir, "dialogue_validations.json");
            var logger = new LogService();

            var service = new DialogueService(logger, dialoguesPath, validationsPath);
            Assert.False(service.IsDialogueValidated("EN", "Play_vo_Ahri_Joke3D"));

            service.SetDialogueValidation("EN", "Play_vo_Ahri_Joke3D", true);
            Assert.True(service.IsDialogueValidated("EN", "Play_vo_Ahri_Joke3D"));

            // Reload service from same files
            var reloadedService = new DialogueService(logger, dialoguesPath, validationsPath);
            Assert.True(reloadedService.IsDialogueValidated("EN", "Play_vo_Ahri_Joke3D"));
            Assert.False(reloadedService.IsDialogueValidated("EN", "Play_vo_Ahri_Taunt3D"));
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
            {
                System.IO.Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void SetDialogueWithEmptyTextRemovesEntry()
    {
        string tempDir = System.IO.Directory.CreateTempSubdirectory("VideoGenerator.DialogueServiceTests.").FullName;
        try
        {
            string dialoguesPath = System.IO.Path.Combine(tempDir, "dialogues.json");
            string validationsPath = System.IO.Path.Combine(tempDir, "dialogue_validations.json");
            var logger = new LogService();

            var service = new DialogueService(logger, dialoguesPath, validationsPath);
            service.SetDialogue("EN", "Play_vo_Ahri_Joke3D", "Hello world");
            Assert.Equal("Hello world", service.GetDialogue("EN", "Play_vo_Ahri_Joke3D"));

            service.SetDialogue("EN", "Play_vo_Ahri_Joke3D", "   ");
            Assert.Equal(string.Empty, service.GetDialogue("EN", "Play_vo_Ahri_Joke3D"));
            Assert.False(service.DialogueExists("EN", "Play_vo_Ahri_Joke3D"));
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
            {
                System.IO.Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
