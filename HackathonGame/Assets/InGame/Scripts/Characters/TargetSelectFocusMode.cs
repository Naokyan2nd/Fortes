/// <summary>
/// ターゲット選択時のステージスクロール方式。
/// </summary>
public enum TargetSelectFocusMode
{
    /// <summary>敵列を中央へ移動し、背景もパララックス（従来）。</summary>
    ScrollEnemyAndParallax = 0,

    /// <summary>敵の位置は固定し、背景パララックスのみ。</summary>
    ParallaxOnly = 1,
}
