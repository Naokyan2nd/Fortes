/// <summary>
/// Remembers whether OutfitScene was opened from Home or Stage (post-ToReady).
/// Drives BackToHomeButton to return to the correct scene and UI state.
/// </summary>
public static class OutfitSceneReturnContext
{
    public enum SourceScene
    {
        None,
        Home,
        StageReady,
    }

    public static SourceScene Source { get; private set; }

    public static void MarkFromHome()
    {
        Source = SourceScene.Home;
        StageSceneReadyResume.Clear();
    }

    public static void MarkFromStageReady(string statusKey = null, string focusedNoiseName = null)
    {
        Source = SourceScene.StageReady;
        StageSceneReadyResume.MarkResumeReadyState(statusKey, focusedNoiseName);
    }

    public static void HandleBackToHome()
    {
        switch (Source)
        {
            case SourceScene.Home:
                StageSceneReadyResume.Clear();
                SceneTransferManager.Instance.ReturnToScene(SceneNames.Home);
                break;
            case SourceScene.StageReady:
                SceneTransferManager.Instance.ReturnToScene(SceneNames.Stage);
                break;
            default:
                StageSceneReadyResume.Clear();
                SceneTransferManager.Instance.GoBack();
                break;
        }

        Source = SourceScene.None;
    }

    public static void Clear()
    {
        Source = SourceScene.None;
    }
}
