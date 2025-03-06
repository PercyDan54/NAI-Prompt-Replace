namespace NAIPromptReplace.Platform;

public interface IPlatformProgressNotifier
{
    void SetProgress(int completed, int total);
    void NotifyCompleted(bool error);
}
