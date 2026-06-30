using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// スキルの対象区分（ターゲット選択の要否）。
/// </summary>
public enum SkillCategory
{
    Attack,
    Heal
}

/// <summary>
/// スキル定義（威力・QTE・ジングルバリアント）。
/// </summary>
[CreateAssetMenu(fileName = "SkillData", menuName = "InGame/Skill Data")]
public sealed class SkillDataSO : ScriptableObject
{
    [SerializeField]
    private string _skillId;

    [SerializeField]
    private SkillCategory _category = SkillCategory.Attack;

    [SerializeField]
    private int _power;

    [SerializeField]
    private int _healPower;

    [SerializeField]
    [Tooltip("QTE ジングルバリアント（合成クリップ・スクロール・オフセット・タイミングのセット）。QTE 開始時にランダム抽選。")]
    private SkillQteJingleVariant[] _qteVariants;

    [SerializeField, HideInInspector, FormerlySerializedAs("_jingleClip")]
    private AudioClip _legacyJingleClip;

    [SerializeField, HideInInspector, FormerlySerializedAs("_qteCombinedClip")]
    private AudioClip _legacyQteCombinedClip;

    [SerializeField, HideInInspector, FormerlySerializedAs("_jingleTimelineOffsetSeconds")]
    private float _legacyJingleTimelineOffsetSeconds;

    [SerializeField, HideInInspector, FormerlySerializedAs("_qteTimings")]
    private QtePointData[] _legacyQteTimings;

    [SerializeField, HideInInspector, FormerlySerializedAs("_noteScrollDurationSeconds")]
    private float _legacyNoteScrollDurationSeconds = 1f;

    [Header("Player attack trail (Attack, straight)")]
    [SerializeField]
    private PlayerAttackTrailSettings _attackTrailSettings = new PlayerAttackTrailSettings();

    [System.NonSerialized]
    private int _lastPickedVariantIndex = -1;

    /// <summary>スキルID。</summary>
    public string SkillId => _skillId;

    /// <summary>カテゴリ。</summary>
    public SkillCategory Category => _category;

    /// <summary>威力（攻撃）。</summary>
    public int Power => _power;

    /// <summary>回復力（回復）。</summary>
    public int HealPower => _healPower;

    /// <summary>QTE ジングルバリアント配列。</summary>
    public IReadOnlyList<SkillQteJingleVariant> QteVariants => _qteVariants;

    /// <summary>攻撃の Trail（直進）設定。</summary>
    public PlayerAttackTrailSettings AttackTrailSettings => _attackTrailSettings;

    /// <summary>QTE バリアント抽選履歴をリセットする（戦闘開始時など）。</summary>
    public void ResetQteVariantHistory()
    {
        _lastPickedVariantIndex = -1;
    }

    /// <summary>
    /// 有効な QTE ジングルバリアントを抽選する。2件以上ある場合は直前に選んだ index を除外する。
    /// </summary>
    public bool TryPickQteVariant(out SkillQteJingleVariant variant)
    {
        variant = null;
        if (_qteVariants == null || _qteVariants.Length == 0)
        {
            return false;
        }

        var validIndices = new List<int>(_qteVariants.Length);
        for (int i = 0; i < _qteVariants.Length; i++)
        {
            SkillQteJingleVariant candidate = _qteVariants[i];
            if (candidate != null && candidate.IsValid)
            {
                validIndices.Add(i);
            }
        }

        if (validIndices.Count == 0)
        {
            return false;
        }

        if (validIndices.Count == 1)
        {
            int onlyIndex = validIndices[0];
            _lastPickedVariantIndex = onlyIndex;
            variant = _qteVariants[onlyIndex];
            return true;
        }

        var pool = new List<int>(validIndices.Count);
        for (int i = 0; i < validIndices.Count; i++)
        {
            int index = validIndices[i];
            if (index != _lastPickedVariantIndex)
            {
                pool.Add(index);
            }
        }

        if (pool.Count == 0)
        {
            pool.AddRange(validIndices);
        }

        int pickedIndex = pool[Random.Range(0, pool.Count)];
        _lastPickedVariantIndex = pickedIndex;
        variant = _qteVariants[pickedIndex];
        return variant != null;
    }

    /// <summary>抽選結果を含む QTE 再生コンテキストを作成する。</summary>
    public bool TryCreateQtePlaybackContext(out SkillQtePlaybackContext context)
    {
        if (!TryPickQteVariant(out SkillQteJingleVariant variant))
        {
            context = null;
            return false;
        }

        context = new SkillQtePlaybackContext(this, variant);
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        MigrateLegacyQteFieldsIfNeeded();
        MigrateVariantFieldsIfNeeded();
    }

    /// <summary>旧フラット QTE フィールドを _qteVariants[0] へ移行する（Editor のみ）。</summary>
    public void MigrateLegacyQteFieldsIfNeeded()
    {
        if (_qteVariants != null && _qteVariants.Length > 0)
        {
            return;
        }

        AudioClip legacyClip = _legacyQteCombinedClip != null ? _legacyQteCombinedClip : _legacyJingleClip;
        if (legacyClip == null && (_legacyQteTimings == null || _legacyQteTimings.Length == 0))
        {
            return;
        }

        SerializedObject so = new SerializedObject(this);
        SerializedProperty variantsProp = so.FindProperty("_qteVariants");
        variantsProp.arraySize = 1;
        SerializedProperty variantProp = variantsProp.GetArrayElementAtIndex(0);
        variantProp.FindPropertyRelative("_qteCombinedClip").objectReferenceValue = legacyClip;
        variantProp.FindPropertyRelative("_noteScrollDurationSeconds").floatValue =
            _legacyNoteScrollDurationSeconds > 0f ? _legacyNoteScrollDurationSeconds : 1f;
        variantProp.FindPropertyRelative("_jingleTimelineOffsetSeconds").floatValue =
            _legacyJingleTimelineOffsetSeconds;
        variantProp.FindPropertyRelative("_qteTimings").arraySize =
            _legacyQteTimings != null ? _legacyQteTimings.Length : 0;
        SerializedProperty timingsProp = variantProp.FindPropertyRelative("_qteTimings");
        for (int i = 0; i < timingsProp.arraySize; i++)
        {
            QtePointData legacyPoint = _legacyQteTimings[i];
            if (legacyPoint == null)
            {
                continue;
            }

            timingsProp.GetArrayElementAtIndex(i).FindPropertyRelative("_timingInSeconds").floatValue =
                legacyPoint.TimingInSeconds;
        }

        ClearLegacyFlatQteFields(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(this);
    }

    /// <summary>バリアント内の旧 JingleClip・スキル共通スクロール秒数を移行する。</summary>
    public void MigrateVariantFieldsIfNeeded()
    {
        if (_qteVariants == null || _qteVariants.Length == 0)
        {
            return;
        }

        SerializedObject so = new SerializedObject(this);
        SerializedProperty variantsProp = so.FindProperty("_qteVariants");
        bool changed = false;
        float legacyScroll = _legacyNoteScrollDurationSeconds > 0f
            ? _legacyNoteScrollDurationSeconds
            : 1f;

        for (int i = 0; i < variantsProp.arraySize; i++)
        {
            SerializedProperty variantProp = variantsProp.GetArrayElementAtIndex(i);
            SerializedProperty combinedProp = variantProp.FindPropertyRelative("_qteCombinedClip");
            SerializedProperty legacyJingleProp = variantProp.FindPropertyRelative("_legacyVariantJingleClip");
            if (combinedProp.objectReferenceValue == null
                && legacyJingleProp != null
                && legacyJingleProp.objectReferenceValue != null)
            {
                combinedProp.objectReferenceValue = legacyJingleProp.objectReferenceValue;
                legacyJingleProp.objectReferenceValue = null;
                changed = true;
            }

            SerializedProperty scrollProp = variantProp.FindPropertyRelative("_noteScrollDurationSeconds");
            if (scrollProp.floatValue <= 0f)
            {
                scrollProp.floatValue = legacyScroll;
                changed = true;
            }
        }

        if (_legacyNoteScrollDurationSeconds > 0f
            || _legacyJingleClip != null
            || _legacyQteCombinedClip != null
            || (_legacyQteTimings != null && _legacyQteTimings.Length > 0))
        {
            ClearLegacyFlatQteFields(so);
            changed = true;
        }

        if (changed)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(this);
        }
    }

    private void ClearLegacyFlatQteFields(SerializedObject so)
    {
        so.FindProperty("_legacyJingleClip").objectReferenceValue = null;
        so.FindProperty("_legacyQteCombinedClip").objectReferenceValue = null;
        so.FindProperty("_legacyJingleTimelineOffsetSeconds").floatValue = 0f;
        so.FindProperty("_legacyQteTimings").arraySize = 0;
        so.FindProperty("_legacyNoteScrollDurationSeconds").floatValue = 0f;
    }
#endif
}
