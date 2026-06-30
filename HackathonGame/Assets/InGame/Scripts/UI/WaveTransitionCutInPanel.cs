using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ウェーブカットインのアニメーション対象。
/// サイズ・Sprite・Image はエディター配置のまま。実行時は表示と Tween のみ。
/// </summary>
[DisallowMultipleComponent]
public sealed class WaveTransitionCutInPanel : MonoBehaviour
{
    [SerializeField]
    private CanvasGroup _canvasGroup;

    private Vector2 _restAnchoredPosition;
    private Vector3 _restLocalPosition;
    private Vector3 _restLocalScale;
    private bool _layoutCaptured;

    public RectTransform Root => transform as RectTransform;

    public Vector2 RestAnchoredPosition =>
        _layoutCaptured && Root != null ? _restAnchoredPosition : Root != null ? Root.anchoredPosition : Vector2.zero;

    public Vector3 RestLocalPosition =>
        _layoutCaptured && Root != null ? _restLocalPosition : Root != null ? Root.localPosition : Vector3.zero;

    public Vector3 RestLocalScale =>
        _layoutCaptured && Root != null ? _restLocalScale : Root != null ? Root.localScale : Vector3.one;

    public CanvasGroup CanvasGroup
    {
        get
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
            }

            return _canvasGroup;
        }
    }

    private void Awake()
    {
        CaptureLayout();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CaptureLayout();
    }
#endif

    /// <summary>ヒエラルキー上の RectTransform を基準レイアウトとして記録する。</summary>
    public void CaptureLayout()
    {
        if (Root == null)
        {
            return;
        }

        _restAnchoredPosition = Root.anchoredPosition;
        _restLocalPosition = Root.localPosition;
        _restLocalScale = Root.localScale;
        _layoutCaptured = true;
    }

    /// <summary>アニメーション後にヒエラルキー基準の位置・スケールへ戻す。</summary>
    public void RestoreLayout()
    {
        if (Root == null || !_layoutCaptured)
        {
            return;
        }

        Root.anchoredPosition = _restAnchoredPosition;
        Root.localPosition = _restLocalPosition;
        Root.localScale = _restLocalScale;
    }

    /// <summary>アニメーション開始前。子 Image / TMP は触らない。レイアウトの記録は Awake 時のみ。</summary>
    public void PrepareForAnimation()
    {
        if (!_layoutCaptured)
        {
            CaptureLayout();
        }

        RestoreLayout();
        gameObject.SetActive(true);

        CanvasGroup group = CanvasGroup;
        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        Image image = GetComponent<Image>();
        if (image != null)
        {
            image.enabled = true;
            Color color = image.color;
            color.a = 1f;
            image.color = color;
        }
    }

    /// <summary>アニメーション終了後の非表示。</summary>
    public void HideAfterAnimation()
    {
        RestoreLayout();

        CanvasGroup group = CanvasGroup;
        if (group != null)
        {
            group.alpha = 0f;
        }
    }
}
