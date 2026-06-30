/// <summary>
/// MainScene バトル結果 → ResultScene の受け渡し。
/// </summary>
public static class BattleResultSession
{
    static bool _hasPending;
    static bool _isVictory;
    static string _stageId;

    public static void SetPending(bool isVictory, string stageId)
    {
        _hasPending = true;
        _isVictory = isVictory;
        _stageId = stageId;
    }

    public static void ClearPending()
    {
        _hasPending = false;
        _stageId = null;
    }

    public static bool TryConsume(out bool isVictory, out string stageId)
    {
        if (!_hasPending)
        {
            isVictory = false;
            stageId = null;
            return false;
        }

        isVictory = _isVictory;
        stageId = _stageId;
        _hasPending = false;
        _stageId = null;
        return true;
    }
}
