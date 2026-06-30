using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 太鼓 QTE の空間ゾーン判定用ノート選択。
/// </summary>
public static class QteTaikoSpatialJudgment
{
    /// <summary>Runner から渡す未判定ノート参照。</summary>
    public readonly struct NoteHandle
    {
        public readonly int Index;
        public readonly float SpawnSec;
        public readonly QteTaikoNoteView View;
        public readonly bool Resolved;

        public NoteHandle(int index, float spawnSec, QteTaikoNoteView view, bool resolved)
        {
            Index = index;
            SpawnSec = spawnSec;
            View = view;
            Resolved = resolved;
        }
    }

    /// <summary>タップ時に判定対象とする未解決ノートを選ぶ（画面外のノートは対象外）。</summary>
    public static bool TrySelectNoteForTap(
        IReadOnlyList<NoteHandle> notes,
        QteTaikoJudgmentZones zones,
        float playViewportXMin,
        float playViewportXMax,
        out NoteHandle selected)
    {
        selected = default;
        if (notes == null || notes.Count == 0 || zones == null)
        {
            return false;
        }

        NoteHandle? bestInGood = null;
        for (int i = 0; i < notes.Count; i++)
        {
            NoteHandle note = notes[i];
            if (note.Resolved || note.View == null)
            {
                continue;
            }

            if (!IsCenterInPlayViewport(note.View.GetCenterAnchoredPosition().x, playViewportXMin, playViewportXMax))
            {
                continue;
            }

            RectTransform noteRect = note.View.RectTransform;
            if (!zones.ContainsGood(noteRect))
            {
                continue;
            }

            if (!bestInGood.HasValue
                || note.SpawnSec < bestInGood.Value.SpawnSec
                || (Math.Abs(note.SpawnSec - bestInGood.Value.SpawnSec) < 1e-9f
                    && note.Index < bestInGood.Value.Index))
            {
                bestInGood = note;
            }
        }

        if (bestInGood.HasValue)
        {
            selected = bestInGood.Value;
            return true;
        }

        return TrySelectLeftmost(notes, playViewportXMin, playViewportXMax, out selected);
    }

    /// <summary>未解決ノートのうち最も左（X 最小）を返す（画面内のみ）。</summary>
    public static bool TrySelectLeftmost(
        IReadOnlyList<NoteHandle> notes,
        float playViewportXMin,
        float playViewportXMax,
        out NoteHandle selected)
    {
        selected = default;
        bool found = false;
        float minX = float.MaxValue;

        for (int i = 0; i < notes.Count; i++)
        {
            NoteHandle note = notes[i];
            if (note.Resolved || note.View == null)
            {
                continue;
            }

            float x = note.View.GetCenterAnchoredPosition().x;
            if (!IsCenterInPlayViewport(x, playViewportXMin, playViewportXMax))
            {
                continue;
            }
            if (!found || x < minX - 1e-5f
                || (Mathf.Abs(x - minX) < 1e-5f && note.Index < selected.Index))
            {
                minX = x;
                selected = note;
                found = true;
            }
        }

        return found;
    }

    /// <summary>ノート中心が NoteParent の表示範囲内か。</summary>
    public static bool IsCenterInPlayViewport(float centerAnchoredX, float playViewportXMin, float playViewportXMax)
    {
        return centerAnchoredX >= playViewportXMin && centerAnchoredX <= playViewportXMax;
    }
}
