using UnityEngine;

/// <summary>
/// バトル全体の調整値（プレイヤー基礎値・QTE倍率）。
/// </summary>
[CreateAssetMenu(fileName = "BattleSettings", menuName = "InGame/Battle Settings")]
public sealed class BattleSettingsSO : ScriptableObject
{
    [SerializeField]
    private int _playerMaxHp = 100;

    [SerializeField]
    private int _playerSpeed = 5;

    [SerializeField]
    private float _qtePerfectMultiplier = 1.3f;

    [SerializeField]
    private float _qteGoodMultiplier = 1.1f;

    [SerializeField]
    [Tooltip("Miss 1 ノートあたりの乗算倍率。Perfect 打ち消しとは別に積算に乗る。")]
    private float _qteMissMultiplier = 1f;

    [SerializeField]
    private float _allPerfectHealBonusMultiplier = 2f;

    [Header("Tutorial (Enrage guide)")]
    [SerializeField]
    [Tooltip("狂暴化チュートリアル表示までのプレイヤー攻撃ダメージ上限（0 なら未使用）。QTE 倍率の結果をこの値でクランプする。")]
    private int _tutorialPlayerDamageCapPerHit;

    [Header("Victory Reward")]
    [SerializeField]
    private int _victoryExp = 120;

    /// <summary>プレイヤー最大HP。</summary>
    public int PlayerMaxHp => _playerMaxHp;

    /// <summary>プレイヤーSpeed（行動順）。</summary>
    public int PlayerSpeed => _playerSpeed;

    /// <summary>QTE Perfect のダメージ／回復倍率。</summary>
    public float QtePerfectMultiplier => _qtePerfectMultiplier;

    /// <summary>QTE Good の倍率。</summary>
    public float QteGoodMultiplier => _qteGoodMultiplier;

    /// <summary>QTE Miss の倍率。</summary>
    public float QteMissMultiplier => _qteMissMultiplier;

    /// <summary>全ノート Perfect 時の回復追加倍率。</summary>
    public float AllPerfectHealBonusMultiplier => _allPerfectHealBonusMultiplier;

    /// <summary>勝利時に獲得する経験値。</summary>
    public int VictoryExp => _victoryExp;

    /// <summary>チュートリアル中（狂暴化説明まで）の1ヒットあたりプレイヤー攻撃ダメージ上限。0 なら未設定。</summary>
    public int TutorialPlayerDamageCapPerHit => Mathf.Max(0, _tutorialPlayerDamageCapPerHit);
}
