namespace VideoGenerator.Services;

public interface IDialogueStore
{
    string GetDialogue(string language, string folderName);

    void SetDialogue(string language, string folderName, string text);

    bool IsDialogueValidated(string language, string folderName);

    void SetDialogueValidation(string language, string folderName, bool isValidated);
}
