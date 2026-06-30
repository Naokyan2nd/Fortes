using UnityEngine;

/// <summary>
/// タイトル初回イントロ（Panel 1〜5）の表示済み状態（PlayerPrefs）。
/// 初回チュートリアル完了時にのみ保存される（途中終了時は次回もイントロを再生）。
/// </summary>
public static class TitleIntroProgress
{
    const string PrefKeyCompleted = "TitleIntro_Completed_v1";

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
