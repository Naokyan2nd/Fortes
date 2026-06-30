using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1ステージ分のウェーブ列。InGameManager はこれを経由して WaveConfigSO を参照する。
/// </summary>
[CreateAssetMenu(fileName = "StageConfig", menuName = "InGame/Stage Config")]
public sealed class StageConfigSO : ScriptableObject
{
    [SerializeField]
    private string _stageId;

    [SerializeField]
    private string _displayName;

    [SerializeField]
    private WaveConfigSO[] _waves;

    [SerializeField]
    private bool _isTutorialStage;

    /// <summary>将来 OutGame 連携用の識別子。</summary>
    public string StageId => _stageId;

    /// <summary>初回チュートリアル用ステージ（報酬・ノイズ撃破を抑制）。</summary>
    public bool IsTutorialStage => _isTutorialStage;

    /// <summary>表示名（ログ・UI 用、任意）。</summary>
    public string DisplayName => _displayName;

    /// <summary>このステージの総ウェーブ数。</summary>
    public int WaveCount => _waves != null ? _waves.Length : 0;

    /// <summary>バリデーションループ用。</summary>
    public IReadOnlyList<WaveConfigSO> Waves => _waves;

    /// <summary>指定インデックスのウェーブ。範囲外は null。</summary>
    public WaveConfigSO GetWave(int index)
    {
        if (_waves == null || index < 0 || index >= _waves.Length)
        {
            return null;
        }

        return _waves[index];
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_waves == null)
        {
            return;
        }

        for (int i = 0; i < _waves.Length; i++)
        {
            WaveConfigSO wave = _waves[i];
            if (wave != null && wave.WaveIndex != i)
            {
                Debug.LogWarning(
                    $"[StageConfigSO] '{name}' waves[{i}] の WaveIndex ({wave.WaveIndex}) が配列インデックスと一致しません。",
                    this);
            }
        }
    }
#endif
}
