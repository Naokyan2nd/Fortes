using UnityEngine;

/// <summary>
/// 敵1種類分のパラメータ。
/// </summary>
[CreateAssetMenu(fileName = "EnemyData", menuName = "InGame/Enemy Data")]
public sealed class EnemyDataSO : ScriptableObject
{
    [SerializeField]
    private string _enemyId;

    [SerializeField]
    private int _maxHp = 30;

    [SerializeField]
    private int _speed = 3;

    [SerializeField]
    private int _attackPower = 5;

    [SerializeField]
    private Sprite _sprite;

    [SerializeField]
    private EnemyView _viewPrefab;

    /// <summary>敵ID。</summary>
    public string EnemyId => _enemyId;

    /// <summary>最大HP。</summary>
    public int MaxHp => _maxHp;

    /// <summary>行動順用Speed。</summary>
    public int Speed => _speed;

    /// <summary>プレイヤーへの攻撃力（固定ダメージ）。</summary>
    public int AttackPower => _attackPower;

    /// <summary>表示用スプライト（設定時は生成後にプレハブ上書き）。</summary>
    public Sprite Sprite => _sprite;

    /// <summary>スポーン用プレハブ（EnemyView 必須）。</summary>
    public EnemyView ViewPrefab => _viewPrefab;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_viewPrefab == null)
        {
            Debug.LogWarning($"[EnemyDataSO] ViewPrefab が未設定です: {name}", this);
        }
    }
#endif
}
