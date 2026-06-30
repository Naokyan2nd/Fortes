/// <summary>
/// Noises のフォーカス種別（StageStatusFocusSync の status key）と StageConfigSO.StageId の対応。
/// </summary>
public static class StageBattleStageIds
{
    public const string NormalStage = "NormalStage";
    public const string RareStage = "RareStage";
    public const string SuperRareStage = "SuperRareStage";

    /// <summary>StageStatus の種別から StageId を解決する。</summary>
    public static bool TryGetStageIdForStatusKey(string statusKey, out string stageId)
    {
        switch (statusKey)
        {
            case "Normal":
                stageId = NormalStage;
                return true;
            case "Rare":
                stageId = RareStage;
                return true;
            case "SuperRare":
                stageId = SuperRareStage;
                return true;
            default:
                stageId = null;
                return false;
        }
    }

    /// <summary>
    /// StageId から Noises 直下の子オブジェクト名へ（単一マッピングの種別のみ）。
    /// Normal は複数子があるため解決できない。
    /// </summary>
    public static bool TryResolveNoiseChildNameForStageId(string stageId, out string noiseChildName)
    {
        if (stageId == SuperRareStage)
        {
            noiseChildName = "SuperRare";
            return true;
        }

        if (stageId == RareStage)
        {
            noiseChildName = "Rare";
            return true;
        }

        noiseChildName = null;
        return false;
    }

    public const string TutorialVictoryPanelName = "InGameTutorial";

    /// <summary>Result パネル直下の表示用オブジェクト名（勝利時）。</summary>
    public static string GetVictoryPanelNameForStageId(string stageId)
    {
        if (TutorialStageIds.IsTutorialStageId(stageId))
        {
            return TutorialVictoryPanelName;
        }

        if (stageId == RareStage)
        {
            return "WinRare";
        }

        if (stageId == SuperRareStage)
        {
            return "WinSupreRare";
        }

        return "WinNormal";
    }

    public const string DefeatPanelName = "Lose";
}
