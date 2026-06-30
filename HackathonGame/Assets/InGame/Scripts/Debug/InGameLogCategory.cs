/// <summary>
/// インゲーム用ログカテゴリ。インスペクターで ON/OFF する（Flags）。
/// </summary>
[System.Flags]
public enum InGameLogCategory
{
    None = 0,

    /// <summary>初期化・ウェーブ前後・ラウンドループなどステートマシン上の位相。</summary>
    StateMachine = 1 << 0,

    /// <summary>ウェーブ開始・敵スポーン。</summary>
    Wave = 1 << 1,

    /// <summary>行動順キューの構築結果。</summary>
    TurnOrder = 1 << 2,

    /// <summary>プレイヤーターン（スキル選択・ターゲット・SP 回復など）。</summary>
    Player = 1 << 3,

    /// <summary>敵ターン（攻撃・撃破除外の宣言）。</summary>
    Enemy = 1 << 4,

    /// <summary>ダメージ・回復・SP 増減の結果数値。</summary>
    Combat = 1 << 5,

    /// <summary>QTE 成否の一覧。</summary>
    Qte = 1 << 6,

    /// <summary>敗北・勝利・次ウェーブへ進む判定。</summary>
    Result = 1 << 7,

    /// <summary>デフォルト: すべて有効。</summary>
    All = StateMachine | Wave | TurnOrder | Player | Enemy | Combat | Qte | Result,
}
