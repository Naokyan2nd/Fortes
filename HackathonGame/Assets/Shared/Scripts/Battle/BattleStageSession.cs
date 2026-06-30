/// <summary>
/// StageScene → MainScene のステージ受け渡し（シーン遷移をまたぐ静的バッファ）。
/// </summary>
public static class BattleStageSession
{
    static string _pendingStageId;
    static string _pendingNoiseChildName;
    static string _committedBattleNoiseChildName;
    static string _lastPlayedStageId;
    static string _lastPlayedNoiseChildName;
    static string _lastRequestedStageId;

    /// <summary>直近の MainScene で使用した StageId（ResultScene 表示用）。</summary>
    public static string LastPlayedStageId => _lastPlayedStageId;

    /// <summary>StageScene で最後にリクエストした StageId（Fallback 実ステージと別）。</summary>
    public static string LastRequestedStageId => _lastRequestedStageId;

    /// <summary>直近の MainScene で戦闘した Noises 子オブジェクト名。</summary>
    public static string LastPlayedNoiseChildName => _lastPlayedNoiseChildName;

    /// <summary>StageScene で戦闘開始時に確定した Noises 子名（勝利時の撃破記録用）。</summary>
    public static string CommittedBattleNoiseChildName => _committedBattleNoiseChildName;

    public static void SetPending(StageConfigSO stage)
    {
        _pendingStageId = stage != null ? stage.StageId : null;
        _pendingNoiseChildName = null;
    }

    public static void SetPendingStageId(string stageId, string noiseChildName = null)
    {
        _pendingStageId = string.IsNullOrEmpty(stageId) ? null : stageId;
        _pendingNoiseChildName = string.IsNullOrEmpty(noiseChildName) ? null : noiseChildName;
        if (!string.IsNullOrEmpty(stageId))
        {
            _lastRequestedStageId = stageId;
        }

        CommitBattleNoiseChildName(noiseChildName);
    }

    /// <summary>
    /// ToReady 時点で確定した Noises 子名（ToBattle 直前のフォーカス喪失対策）。
    /// </summary>
    public static void CommitBattleNoiseChildName(string noiseChildName)
    {
        if (!string.IsNullOrEmpty(noiseChildName))
        {
            _committedBattleNoiseChildName = noiseChildName;
        }
    }

    public static void ClearCommittedBattleNoise()
    {
        _committedBattleNoiseChildName = null;
    }

    public static void ClearPending()
    {
        _pendingStageId = null;
        _pendingNoiseChildName = null;
    }

    public static bool TryConsume(out string stageId)
    {
        stageId = _pendingStageId;
        _pendingStageId = null;
        return !string.IsNullOrEmpty(stageId);
    }

    public static string ConsumePendingNoiseChildName()
    {
        string noiseChildName = _pendingNoiseChildName;
        _pendingNoiseChildName = null;
        return noiseChildName;
    }

    public static void RecordPlayedBattle(string stageId, string noiseChildName)
    {
        if (!string.IsNullOrEmpty(stageId))
        {
            _lastPlayedStageId = stageId;
        }

        if (!string.IsNullOrEmpty(noiseChildName))
        {
            _lastPlayedNoiseChildName = noiseChildName;
        }
    }

    public static void RecordPlayedStage(string stageId)
    {
        RecordPlayedBattle(stageId, null);
    }
}
