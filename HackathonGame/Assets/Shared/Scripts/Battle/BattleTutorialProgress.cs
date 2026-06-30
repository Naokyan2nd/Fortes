using UnityEngine;

/// <summary>
/// 初回バトルチュートリアル完了状態（PlayerPrefs）。
/// </summary>
public static class BattleTutorialProgress
{
    const string PrefKeyCompleted = "BattleTutorial_Completed_v1";

    public static bool IsCompleted => PlayerPrefs.GetInt(PrefKeyCompleted, 0) != 0;

    public static void MarkCompleted()
    {
        PlayerPrefs.SetInt(PrefKeyCompleted, 1);
        PlayerPrefs.Save();
    }

    public static void ResetForDebug()
    {
        PlayerPrefs.DeleteKey(PrefKeyCompleted);
        PlayerPrefs.Save();
    }
}
