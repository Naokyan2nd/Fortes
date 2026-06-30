using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1ウェーブ分の出現敵リスト。
/// </summary>
[CreateAssetMenu(fileName = "WaveConfig", menuName = "InGame/Wave Config")]
public sealed class WaveConfigSO : ScriptableObject
{
    [SerializeField]
    private int _waveIndex;

    [SerializeField]
    private EnemyDataSO[] _enemies;

    [SerializeField]
    private BattleTutorialStepSO[] _tutorialSteps;

    /// <summary>ウェーブ番号（0始まり想定）。</summary>
    public int WaveIndex => _waveIndex;

    /// <summary>このウェーブで出る敵（最大3想定、可変）。</summary>
    public EnemyDataSO[] Enemies => _enemies;

    /// <summary>このウェーブのチュートリアルステップ（未設定時は空）。</summary>
    public IReadOnlyList<BattleTutorialStepSO> TutorialSteps => _tutorialSteps;

    /// <summary>指定タイミングのステップを Moment 昇順・配列順で返す。</summary>
    public IReadOnlyList<BattleTutorialStepSO> GetTutorialStepsForMoment(WaveTutorialMoment moment)
    {
        if (_tutorialSteps == null || _tutorialSteps.Length == 0)
        {
            return System.Array.Empty<BattleTutorialStepSO>();
        }

        var list = new List<BattleTutorialStepSO>();
        for (int i = 0; i < _tutorialSteps.Length; i++)
        {
            BattleTutorialStepSO step = _tutorialSteps[i];
            if (step != null && step.Moment == moment)
            {
                list.Add(step);
            }
        }

        return list;
    }
}
