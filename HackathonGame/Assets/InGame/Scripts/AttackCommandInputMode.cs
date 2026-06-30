/// <summary>
/// 攻撃コマンドの入力方式（InGameManager で切り替え）。
/// </summary>
public enum AttackCommandInputMode
{
    /// <summary>1回目で選択、2回目で確定＋ターゲット選択（従来）。</summary>
    TwoStep = 0,

    /// <summary>攻撃ボタン1回で、最左の生存敵をターゲットに即確定。</summary>
    QuickConfirm = 1,
}
