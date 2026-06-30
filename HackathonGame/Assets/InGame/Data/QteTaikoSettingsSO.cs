using UnityEngine;

/// <summary>
/// 太鼓風スクロール QTE のレイアウト設定（速度・スポーンはランタイム自動計算）。
/// </summary>
[CreateAssetMenu(fileName = "QteTaikoSettings", menuName = "InGame/QTE Taiko Settings")]
public sealed class QteTaikoSettingsSO : ScriptableObject
{
    private const int MinNoteCount = 1;
    private const int MaxNoteCount = 15;

    [SerializeField]
    [Tooltip("Perfect 到達 X（NoteParent ローカル。通常 0 = 中央）。判定帯はこの X を中心に配置する。")]
    private float _judgmentLineX = 0f;

    [SerializeField]
    [Tooltip("音符のレーン Y（NoteParent ローカル）。")]
    private float _noteLaneY = 0f;

    [SerializeField]
    [Tooltip("全音符の判定が終わった後、QTE を閉じるまでの余韻（秒）。")]
    private float _postRollSeconds = 0.15f;

    [SerializeField]
    [Tooltip("Good 帯通過ミス時のノートフェードアウト時間（秒）。")]
    private float _missFadeOutSeconds = 0.3f;

    [Header("Note visuals")]
    [SerializeField]
    [Tooltip("攻撃スキル QTE の音符スプライト。")]
    private Sprite _attackNoteSprite;

    [SerializeField]
    [Tooltip("回復スキル QTE の音符スプライト。")]
    private Sprite _healNoteSprite;

    [Header("Hit effect visuals")]
    [SerializeField]
    [Tooltip("攻撃スキル QTE のタップヒットエフェクトスプライト。")]
    private Sprite _attackHitEffectSprite;

    [SerializeField]
    [Tooltip("回復スキル QTE のタップヒットエフェクトスプライト。")]
    private Sprite _healHitEffectSprite;

    [SerializeField]
    [Tooltip("ストック UI に格納する Miss アイコンのスプライト。")]
    private Sprite _missStockSprite;

    [Header("Perfect streak SE pitch")]
    [SerializeField]
    [Tooltip("Perfect 連続で QteTap（タップ音）SE のピッチを上げる。")]
    private bool _enablePerfectStreakPitch = true;

    [SerializeField]
    [Tooltip("連続 Perfect ごとに QteTap の Catalog ベース pitch へ加算する量。")]
    [Range(0f, 0.2f)]
    private float _perfectStreakPitchStep = 0.05f;

    [SerializeField]
    [Tooltip("QteTap（タップ音）のピッチ上限。")]
    [Range(1f, 2f)]
    private float _perfectStreakPitchMax = 1.3f;

    /// <summary>同時スクロール可能な音符数の下限。</summary>
    public static int SimultaneousNoteMin => MinNoteCount;

    /// <summary>同時スクロール可能な音符数の上限。</summary>
    public static int SimultaneousNoteMax => MaxNoteCount;

    /// <summary>判定 X。</summary>
    public float JudgmentLineX => _judgmentLineX;

    /// <summary>レーン Y。</summary>
    public float NoteLaneY => _noteLaneY;

    /// <summary>ポストロール秒。</summary>
    public float PostRollSeconds => _postRollSeconds;

    /// <summary>通過 Miss 時のフェードアウト秒。</summary>
    public float MissFadeOutSeconds => _missFadeOutSeconds;

    /// <summary>スキルカテゴリに応じた音符スプライト。</summary>
    public Sprite GetNoteSprite(SkillCategory category)
    {
        return category == SkillCategory.Heal ? _healNoteSprite : _attackNoteSprite;
    }

    /// <summary>スキルカテゴリに応じたタップヒットエフェクトスプライト。</summary>
    public Sprite GetHitEffectSprite(SkillCategory category)
    {
        return category == SkillCategory.Heal ? _healHitEffectSprite : _attackHitEffectSprite;
    }

    /// <summary>ストック UI 用の Miss アイコンスプライト。</summary>
    public Sprite GetMissStockSprite()
    {
        return _missStockSprite;
    }

    /// <summary>
    /// 連続 Perfect 数に応じた QteTap（タップ音）の再生ピッチ。
    /// </summary>
    /// <param name="streak">1 以上の連続 Perfect 数。</param>
    /// <param name="catalogBasePitch">SE Catalog の QteTap の PlaybackPitch。</param>
    public float GetPerfectJudgmentPitch(int streak, float catalogBasePitch)
    {
        if (!_enablePerfectStreakPitch || streak <= 1)
        {
            return Mathf.Clamp(catalogBasePitch, 0.01f, 3f);
        }

        float maxPitch = Mathf.Max(catalogBasePitch, _perfectStreakPitchMax);
        float pitch = catalogBasePitch + _perfectStreakPitchStep * (streak - 1);
        return Mathf.Clamp(pitch, 0.01f, maxPitch);
    }
}
