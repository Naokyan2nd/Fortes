using UnityEngine;

/// <summary>
/// 判定確定時（ノーツ返却前）に確定した吸収フライ用座標スナップショット。
/// </summary>
public readonly struct QteAbsorbFlightSnapshot
{
    public readonly int NoteIndex;
    public readonly QteJudgment Judgment;
    public readonly Sprite NoteSprite;
    public readonly Vector3 WorldSpawn;
    public readonly bool HasValidAnchors;
    public readonly Vector2 AnchoredStart;
    public readonly Vector2 AnchoredEnd;

    public QteAbsorbFlightSnapshot(
        int noteIndex,
        QteJudgment judgment,
        Sprite noteSprite,
        Vector3 worldSpawn,
        bool hasValidAnchors,
        Vector2 anchoredStart,
        Vector2 anchoredEnd)
    {
        NoteIndex = noteIndex;
        Judgment = judgment;
        NoteSprite = noteSprite;
        WorldSpawn = worldSpawn;
        HasValidAnchors = hasValidAnchors;
        AnchoredStart = anchoredStart;
        AnchoredEnd = anchoredEnd;
    }
}
