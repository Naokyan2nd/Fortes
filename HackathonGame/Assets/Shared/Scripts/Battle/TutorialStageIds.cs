/// <summary>
/// チュートリアル専用ステージ ID（本番 Noises マップとは別管理）。
/// </summary>
public static class TutorialStageIds
{
    public const string TutorialStage = "TutorialStage";

    public static bool IsTutorialStageId(string stageId)
    {
        return stageId == TutorialStage;
    }
}
