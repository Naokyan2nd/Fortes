/// <summary>
/// Remembers that StageScene should reopen in post-ToReady state (e.g. returning from OutfitScene).
/// </summary>
public static class StageSceneReadyResume
{
    public static bool ResumeReadyStateOnNextLoad { get; private set; }
    public static string SavedStatusKey { get; private set; }
    public static string SavedFocusedNoiseName { get; private set; }

    public static void MarkResumeReadyState(string statusKey = null, string focusedNoiseName = null)
    {
        ResumeReadyStateOnNextLoad = true;
        SavedStatusKey = statusKey;
        SavedFocusedNoiseName = focusedNoiseName;
    }

    public static bool ConsumeResumeReadyState(out string statusKey, out string focusedNoiseName)
    {
        statusKey = SavedStatusKey;
        focusedNoiseName = SavedFocusedNoiseName;

        if (!ResumeReadyStateOnNextLoad)
        {
            return false;
        }

        ResumeReadyStateOnNextLoad = false;
        SavedStatusKey = null;
        SavedFocusedNoiseName = null;
        return true;
    }

    public static void Clear()
    {
        ResumeReadyStateOnNextLoad = false;
        SavedStatusKey = null;
        SavedFocusedNoiseName = null;
    }
}
