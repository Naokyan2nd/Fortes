using UnityEngine;

/// <summary>
/// 太鼓 QTE の Perfect / Good 判定帯（RectTransform ・同一 Canvas 空間）。
/// </summary>
public sealed class QteTaikoJudgmentZones : MonoBehaviour
{
    [SerializeField]
    private RectTransform _perfectZone;

    [SerializeField]
    private RectTransform _goodZone;

    /// <summary>Perfect 帯。</summary>
    public RectTransform PerfectZone => _perfectZone;

    /// <summary>Good 帯（Perfect を包含する想定）。</summary>
    public RectTransform GoodZone => _goodZone;

    /// <summary>ノート中心が Perfect 帯内か。</summary>
    public bool ContainsPerfect(RectTransform noteRect)
    {
        return noteRect != null && _perfectZone != null && ContainsNoteCenter(_perfectZone, noteRect);
    }

    /// <summary>ノート中心が Good 帯内か。</summary>
    public bool ContainsGood(RectTransform noteRect)
    {
        return noteRect != null && _goodZone != null && ContainsNoteCenter(_goodZone, noteRect);
    }

    /// <summary>ノート中心の位置から Perfect / Good / Miss を判定する。</summary>
    public QteJudgment JudgeByCenter(RectTransform noteRect)
    {
        if (noteRect == null || _goodZone == null || _perfectZone == null)
        {
            return QteJudgment.Miss;
        }

        if (ContainsPerfect(noteRect))
        {
            return QteJudgment.Perfect;
        }

        if (ContainsGood(noteRect))
        {
            return QteJudgment.Good;
        }

        return QteJudgment.Miss;
    }

    /// <summary>ノート中心が Good 帯の左端を過ぎた（通過ミス）か。</summary>
    public bool HasPassedGoodZone(RectTransform noteRect)
    {
        if (noteRect == null || _goodZone == null)
        {
            return false;
        }

        if (!TryGetNoteCenterInZoneLocal(_goodZone, noteRect, out Vector2 local))
        {
            return false;
        }

        return local.x < _goodZone.rect.xMin;
    }

    /// <summary>帯レイアウトを検証する。</summary>
    public bool TryValidateLayout(float judgmentLineX, out string errorMessage)
    {
        errorMessage = null;
        if (_perfectZone == null || _goodZone == null)
        {
            errorMessage = "[QteTaiko] Perfect / Good 判定帯が未設定です。";
            return false;
        }

        if (_perfectZone.rect.width <= 0f || _goodZone.rect.width <= 0f)
        {
            errorMessage = "[QteTaiko] 判定帯の幅が 0 です。";
            return false;
        }

        if (_goodZone.rect.width < _perfectZone.rect.width - 1e-3f)
        {
            errorMessage = "[QteTaiko] Good 帯は Perfect 帯より広い必要があります。";
            return false;
        }

        Vector3 lineWorld = _goodZone.parent != null
            ? _goodZone.parent.TransformPoint(new Vector3(judgmentLineX, 0f, 0f))
            : new Vector3(judgmentLineX, 0f, 0f);

        Camera cam = GetCanvasCamera(_goodZone);
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, lineWorld);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_goodZone, screen, cam, out Vector2 lineLocal))
        {
            errorMessage = "[QteTaiko] 判定ライン座標の変換に失敗しました。";
            return false;
        }

        if (lineLocal.x < _goodZone.rect.xMin || lineLocal.x > _goodZone.rect.xMax)
        {
            errorMessage =
                $"[QteTaiko] JudgmentLineX={judgmentLineX} が Good 帯の水平範囲外です。帯の位置を調整してください。";
            return false;
        }

        return true;
    }

    private static bool ContainsNoteCenter(RectTransform zone, RectTransform noteRect)
    {
        return TryGetNoteCenterInZoneLocal(zone, noteRect, out Vector2 local)
            && zone.rect.Contains(local);
    }

    private static bool TryGetNoteCenterInZoneLocal(
        RectTransform zone,
        RectTransform noteRect,
        out Vector2 localPoint)
    {
        localPoint = default;
        if (zone == null || noteRect == null)
        {
            return false;
        }

        Vector3 centerWorld = noteRect.TransformPoint(noteRect.rect.center);
        Camera cam = GetCanvasCamera(zone);
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, centerWorld);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(zone, screen, cam, out localPoint);
    }

    private static Camera GetCanvasCamera(RectTransform rect)
    {
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera;
    }
}
