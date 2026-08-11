namespace VideoGenerator.Services;

public interface IDialogueStore
{
    string GetDialogue(string language, string folderName);

    void SetDialogue(string language, string folderName, string text);
}
