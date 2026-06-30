using System;
using UnityEngine;

/// <summary>
/// ステージ種別ごとにクリア時に付与する報酬量（HomeSceneManager Inspector で調整）。
/// </summary>
[Serializable]
public sealed class StageVictoryRewardSettings
{
    [Header("SuperRare Stage Clear")]
    [SerializeField] private BattleRewardCounts _superRareStageVictory = new(5, 5, 0);

    [Header("Rare Stage Clear")]
    [SerializeField] private BattleRewardCounts _rareStageVictory = new(0, 5, 5);

    [Header("Normal Stage Clear")]
    [SerializeField] private BattleRewardCounts _normalStageVictory = new(0, 0, 5);

    public BattleRewardCounts SuperRareStageVictory => _superRareStageVictory;
    public BattleRewardCounts RareStageVictory => _rareStageVictory;
    public BattleRewardCounts NormalStageVictory => _normalStageVictory;

    public static StageVictoryRewardSettings CreateDefaults()
    {
        return new StageVictoryRewardSettings();
    }

    public BattleRewardCounts GetVictoryRewardsForStageId(string stageId)
    {
        if (TutorialStageIds.IsTutorialStageId(stageId))
        {
            return default;
        }

        if (stageId == StageBattleStageIds.SuperRareStage)
        {
            return _superRareStageVictory;
        }

        if (stageId == StageBattleStageIds.RareStage)
        {
            return _rareStageVictory;
        }

        return _normalStageVictory;
    }

    public void CopyFrom(StageVictoryRewardSettings other)
    {
        if (other == null)
        {
            return;
        }

        _superRareStageVictory = other._superRareStageVictory;
        _rareStageVictory = other._rareStageVictory;
        _normalStageVictory = other._normalStageVictory;
    }
}
