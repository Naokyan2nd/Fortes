using System;

using System.Threading;

using Cysharp.Threading.Tasks;

using DG.Tweening;

using UnityEngine;

using UnityEngine.UI;



/// <summary>

/// QTE 倍率吸収演出用のノーツ画像クローン（プール用）。

/// </summary>

[RequireComponent(typeof(CanvasGroup))]

public sealed class QteAbsorbNoteIconView : MonoBehaviour

{

    [SerializeField]

    private RectTransform _rectTransform;



    [SerializeField]

    private CanvasGroup _canvasGroup;



    [SerializeField]

    private Image _noteImage;



    private Sequence _absorbSequence;

    private bool _absorbCompleted;

    private Action _pendingOnComplete;



    /// <summary>吸収 Tween に使う RectTransform。</summary>

    public RectTransform IconRect

    {

        get

        {

            if (_rectTransform == null)

            {

                _rectTransform = transform as RectTransform;

            }



            return _rectTransform;

        }

    }



    private void Awake()

    {

        if (_canvasGroup == null)

        {

            _canvasGroup = GetComponent<CanvasGroup>();

        }



        if (_rectTransform == null)

        {

            _rectTransform = transform as RectTransform;

        }



        if (_noteImage == null)

        {

            _noteImage = GetComponent<Image>();

        }

    }



    private void OnDestroy()

    {

        KillTweens(completeCallback: false);

    }



    /// <summary>スプライトを差し替える。</summary>

    public void ApplySprite(Sprite sprite)

    {

        if (_noteImage == null)

        {

            _noteImage = GetComponent<Image>();

        }



        if (_noteImage != null && sprite != null)

        {

            _noteImage.sprite = sprite;

            _noteImage.enabled = true;

            _noteImage.color = Color.white;

        }

    }



    /// <summary>プール返却前のリセット。</summary>

    /// <summary>吸収 Tween 開始直前の見た目リセット（プール再利用・直列 2 本目以降用）。</summary>

    public void PrepareForAbsorbFlight()

    {

        _pendingOnComplete = null;

        KillTweens(completeCallback: false);

        _absorbCompleted = false;



        if (_canvasGroup != null)

        {

            _canvasGroup.alpha = 1f;

        }



        RectTransform rt = IconRect;

        if (rt != null)

        {

            rt.localScale = Vector3.one;

            rt.localRotation = Quaternion.identity;

        }

    }



    /// <summary>プール返却・破棄時。完了コールバックは呼ばない。</summary>

    public void CancelAbsorbSilently()

    {

        Action onComplete = _pendingOnComplete;

        _pendingOnComplete = null;

        KillTweens(completeCallback: false);

        if (!_absorbCompleted && onComplete != null)

        {

            CompleteAbsorb(onComplete);

        }

    }



    public void ResetForPool()

    {

        _absorbCompleted = true;

        _pendingOnComplete = null;

        KillTweens(completeCallback: false);



        if (_canvasGroup != null)

        {

            _canvasGroup.alpha = 1f;

        }



        RectTransform rt = IconRect;

        if (rt != null)

        {

            rt.localScale = Vector3.one;

            rt.localRotation = Quaternion.identity;

        }



        gameObject.SetActive(false);

    }



    /// <summary>親 Rect 上の anchoredPosition へ吸収する Tween を開始する。</summary>

    public UniTask PlayAbsorbToAnchoredAsync(

        Vector2 anchoredStart,

        Vector2 anchoredEnd,

        float duration,

        float startScale,

        float endScale,

        Ease scaleEase,

        float scaleDelayRatio,

        float scaleDurationRatio,

        float spinRotations,

        int trailSparkleCount,

        float trailSpawnInterval,

        Action<Vector2> onSpawnTrailSparkle,

        CancellationToken token)

    {

        var tcs = new UniTaskCompletionSource();

        PlayAbsorbToAnchored(

            anchoredStart,

            anchoredEnd,

            duration,

            startScale,

            endScale,

            scaleEase,

            scaleDelayRatio,

            scaleDurationRatio,

            spinRotations,

            trailSparkleCount,

            trailSpawnInterval,

            onSpawnTrailSparkle,

            () => tcs.TrySetResult());

        return tcs.Task.AttachExternalCancellation(token);

    }



    public void PlayAbsorbToAnchored(

        Vector2 anchoredStart,

        Vector2 anchoredEnd,

        float duration,

        float startScale,

        float endScale,

        Ease scaleEase,

        float scaleDelayRatio,

        float scaleDurationRatio,

        float spinRotations,

        int trailSparkleCount,

        float trailSpawnInterval,

        Action<Vector2> onSpawnTrailSparkle,

        Action onComplete)

    {

        _pendingOnComplete = null;

        KillTweens(completeCallback: false);

        _absorbCompleted = false;

        _pendingOnComplete = onComplete;

        gameObject.SetActive(true);



        RectTransform rt = IconRect;

        if (rt == null)

        {

            CompleteAbsorb(onComplete);

            return;

        }



        if (duration <= 0.0001f)

        {

            CompleteAbsorb(onComplete);

            return;

        }

        if (_canvasGroup == null)

        {

            _canvasGroup = GetComponent<CanvasGroup>();

        }



        if (_canvasGroup != null)

        {

            _canvasGroup.alpha = 1f;

        }



        rt.anchoredPosition = anchoredStart;

        rt.localRotation = Quaternion.identity;

        rt.localScale = Vector3.one * startScale;

        Vector3 endScaleVec = Vector3.one * endScale;



        _absorbSequence = DOTween.Sequence();

        _absorbSequence.SetUpdate(true);

        _absorbSequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        float clampedDelayRatio = Mathf.Clamp01(scaleDelayRatio);

        float clampedDurationRatio = Mathf.Clamp(scaleDurationRatio, 0.1f, 1f);

        float scaleDelay = duration * clampedDelayRatio;

        float scaleDuration = Mathf.Max(duration * clampedDurationRatio, 0.01f);



        _absorbSequence.Append(rt.DOAnchorPos(anchoredEnd, duration).SetEase(Ease.InQuad));

        _absorbSequence.Join(

            rt.DOScale(endScaleVec, scaleDuration)

                .SetDelay(scaleDelay)

                .SetEase(scaleEase));

        if (_canvasGroup != null)

        {

            _absorbSequence.Join(_canvasGroup.DOFade(0f, duration).SetEase(Ease.Linear));

        }



        if (Mathf.Abs(spinRotations) > 0.0001f)

        {

            _absorbSequence.Join(

                rt.DORotate(

                        new Vector3(0f, 0f, spinRotations * 360f),

                        duration,

                        RotateMode.FastBeyond360)

                    .SetEase(Ease.Linear));

        }



        AppendTrailSparkleCallbacks(

            rt,

            duration,

            trailSparkleCount,

            trailSpawnInterval,

            onSpawnTrailSparkle);



        _absorbSequence.OnComplete(() => CompleteAbsorb(_pendingOnComplete));

        _absorbSequence.OnKill(() =>

        {

            _absorbSequence = null;

            if (!_absorbCompleted && _pendingOnComplete != null)

            {

                CompleteAbsorb(_pendingOnComplete);

            }

        });

    }



    /// <summary>進行中の Tween を停止する。</summary>

    public void KillTweens(bool completeCallback)

    {

        if (_absorbSequence != null && _absorbSequence.IsActive())

        {

            _absorbSequence.Kill(complete: completeCallback);

            if (completeCallback)

            {

                _absorbCompleted = true;

            }

        }



        _absorbSequence = null;



        RectTransform rt = IconRect;

        if (rt != null)

        {

            rt.DOKill(false);

        }



        if (_canvasGroup != null)

        {

            _canvasGroup.DOKill(false);

        }

    }



    private void CompleteAbsorb(Action onComplete)

    {

        if (_absorbCompleted)

        {

            return;

        }



        _absorbCompleted = true;

        _absorbSequence = null;

        _pendingOnComplete = null;

        onComplete?.Invoke();

    }



    private void AppendTrailSparkleCallbacks(

        RectTransform rt,

        float duration,

        int trailSparkleCount,

        float trailSpawnInterval,

        Action<Vector2> onSpawnTrailSparkle)

    {

        if (trailSparkleCount <= 0 || onSpawnTrailSparkle == null || rt == null)

        {

            return;

        }



        float interval = trailSpawnInterval;

        if (interval <= 0f)

        {

            interval = duration / trailSparkleCount;

        }



        interval = Mathf.Max(interval, 0.001f);



        for (int i = 0; i < trailSparkleCount; i++)

        {

            float spawnTime = i * interval;

            if (spawnTime >= duration)

            {

                break;

            }



            _absorbSequence.InsertCallback(spawnTime, () =>

            {

                if (_absorbCompleted || rt == null)

                {

                    return;

                }



                onSpawnTrailSparkle(rt.anchoredPosition);

            });

        }

    }

}


