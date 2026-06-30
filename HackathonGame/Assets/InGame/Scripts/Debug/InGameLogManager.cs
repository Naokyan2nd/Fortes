using UnityEngine;

/// <summary>
/// インゲーム用のカテゴリ別ログ。シーンに配置し、有効カテゴリをインスペクターで切り替える。
/// </summary>
public sealed class InGameLogManager : MonoBehaviour
{
    [SerializeField]
    private InGameLogCategory _enabledCategories = InGameLogCategory.All;

    [SerializeField]
    private bool _logObjectContext = true;

    /// <summary>
    /// 指定カテゴリが有効か（単一ビットのみ渡す想定）。
    /// </summary>
    public bool IsEnabled(InGameLogCategory category)
    {
        if (category == InGameLogCategory.None)
        {
            return false;
        }

        return (_enabledCategories & category) != 0;
    }

    /// <summary>
    /// カテゴリが有効なときだけ Unity ログに出す。
    /// </summary>
    public void Log(InGameLogCategory category, string message)
    {
        if (!IsEnabled(category))
        {
            return;
        }

        string line = "[InGame][" + category + "] " + message;
        if (_logObjectContext)
        {
            Debug.Log(line, this);
        }
        else
        {
            Debug.Log(line);
        }
    }

    /// <summary>
    /// ステートマシン用の簡易ログ（StateMachine カテゴリ）。
    /// </summary>
    public void LogState(string phase, string detail = null)
    {
        if (!IsEnabled(InGameLogCategory.StateMachine))
        {
            return;
        }

        string body = string.IsNullOrEmpty(detail) ? phase : phase + " | " + detail;
        Log(InGameLogCategory.StateMachine, body);
    }
}
