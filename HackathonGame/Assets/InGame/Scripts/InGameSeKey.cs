/// <summary>
/// インゲーム SE の識別子（文字列キー）。コードからの呼び出し用定数。
/// </summary>
public static class InGameSeKey
{
    public const string QteTap = "QteTap";
    /// <summary>QTE 開始時の BGM スクラッチ用 SE（きゅいん等）。Catalog に Clip を割り当てる。</summary>
    public const string QteBgmScratch = "QteBgmScratch";
    public const string QteJudgePerfect = "QteJudgePerfect";
    public const string QteJudgeGood = "QteJudgeGood";
    public const string QteJudgeMiss = "QteJudgeBad";

    public const string UiCommandSelect = "UiCommandSelect";
    public const string UiCommandConfirm = "UiCommandConfirm";
    public const string UiCommandBack = "UiCommandBack";
    public const string UiTargetSwitch = "UiTargetSwitch";
    public const string UiTargetConfirm = "UiTargetConfirm";

    public const string CombatPlayerHit = "CombatPlayerHit";
    public const string CombatEnemyDefeat = "CombatEnemyDefeat";
    public const string CombatPlayerHeal = "CombatPlayerHeal";
    public const string CombatSpGain = "CombatSpGain";
    public const string CombatPlayerDamage = "CombatPlayerDamage";

    public const string BattleWaveStart = "BattleWaveStart";
    public const string BattleWaveClear = "BattleWaveClear";

    /// <summary>チュートリアル入場 WARNING オープニング表示時。Catalog に Clip を割り当てる。</summary>
    public const string TutorialOpeningWarning = "TutorialOpeningWarning";

    /// <summary>チュートリアルポップアップの「次へ」押下時。</summary>
    public const string UiTutorialNext = "UiTutorialNext";

    public const string ResultVictory = "ResultVictory";
    public const string ResultDefeat = "ResultDefeat";

    /// <summary>Catalog シード・レガシー移行用の既定 id 一覧。</summary>
    public static readonly string[] AllKeys =
    {
        QteTap,
        QteBgmScratch,
        QteJudgePerfect,
        QteJudgeGood,
        QteJudgeMiss,
        UiCommandSelect,
        UiCommandConfirm,
        UiCommandBack,
        UiTargetSwitch,
        UiTargetConfirm,
        CombatPlayerHit,
        CombatEnemyDefeat,
        CombatPlayerHeal,
        CombatSpGain,
        CombatPlayerDamage,
        BattleWaveStart,
        BattleWaveClear,
        TutorialOpeningWarning,
        UiTutorialNext,
        ResultVictory,
        ResultDefeat,
    };

    /// <summary>QTE 判定を対応する SE キーに変換する。</summary>
    public static string FromQteJudgment(QteJudgment judgment)
    {
        if (judgment == QteJudgment.Perfect)
        {
            return QteJudgePerfect;
        }

        if (judgment == QteJudgment.Good)
        {
            return QteJudgeGood;
        }

        return QteJudgeMiss;
    }
}
