using System;
using UnityEngine;

/// <summary>
/// Good / Perfect ごとの吸収 Sparkle 演出パラメータ。
/// </summary>
[Serializable]
public struct QteAbsorbSparkleJudgmentSettings
{
    [InspectorName("トレイル数")]
    [Range(0, 40)]
    public int TrailSparkleCount;

    [InspectorName("合流放射数")]
    [Range(0, 40)]
    public int MergeRadialSparkleCount;

    [InspectorName("トレイル開始スケール倍率")]
    [Range(0.1f, 8f)]
    public float TrailStartScaleMultiplier;

    [InspectorName("合流放射開始スケール倍率")]
    [Range(0.1f, 8f)]
    public float MergeRadialStartScaleMultiplier;

    [InspectorName("合流放射飛距離（最小）")]
    [Range(0f, 200f)]
    public float MergeRadialBurstDistanceMin;

    [InspectorName("合流放射飛距離（最大）")]
    [Range(0f, 200f)]
    public float MergeRadialBurstDistanceMax;
}
