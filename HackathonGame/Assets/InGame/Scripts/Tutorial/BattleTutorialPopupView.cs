using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// バトルチュートリアル用モーダルポップアップ（1ステップ表示）。
/// </summary>
public sealed class BattleTutorialPopupView : MonoBehaviour
{
    [SerializeField]
    private GameObject _root;

    [SerializeField]
    private TMP_Text _titleText;

    [SerializeField]
    private TMP_Text _bodyText;

    [SerializeField]
    [Tooltip("動画・イラスト配置の親。未設定なら LoopVideo または最初の Illustration Image の親を使用。")]
    private RectTransform _illustrationMediaHost;

    [SerializeField]
    [Tooltip("StepId とシーン上 Image の対応。スプライトは Image に設定済みとし、enabled で表示切替。")]
    private BattleTutorialStepIllustrationBinding[] _stepIllustrationBindings;

    [SerializeField]
    private RawImage _loopVideoImage;

    [SerializeField]
    private VideoPlayer _loopVideoPlayer;

    [Tooltip("TutorialVideo など。未設定ならシーンの VideoPlayer / RawImage に割り当て済みの RT を使用。")]
    [SerializeField]
    private RenderTexture _sharedVideoRenderTexture;

    [SerializeField]
    private Button _nextButton;

    [SerializeField]
    private TMP_Text _nextButtonLabel;

    private RenderTexture _loopVideoRenderTexture;
    private bool _ownsRenderTexture;
    private VideoClip _preparedClip;
    private UniTaskCompletionSource _advanceTcs;

    /// <summary>ポップアップ表示前に動画をデコードしておく（①表示中などに呼ぶ）。</summary>
    public UniTask PrewarmLoopVideoAsync(VideoClip clip, CancellationToken token)
    {
        return PrepareLoopVideoAsync(clip, token);
    }

    public void Configure(
        GameObject root,
        TMP_Text titleText,
        TMP_Text bodyText,
        Button nextButton,
        TMP_Text nextButtonLabel,
        RawImage loopVideoImage = null,
        VideoPlayer loopVideoPlayer = null,
        RectTransform illustrationMediaHost = null)
    {
        _root = root;
        _titleText = titleText;
        _bodyText = bodyText;
        _illustrationMediaHost = illustrationMediaHost;
        _loopVideoImage = loopVideoImage;
        _loopVideoPlayer = loopVideoPlayer;
        _nextButton = nextButton;
        _nextButtonLabel = nextButtonLabel;
        ApplyBodyTextSettings();
        if (_nextButton != null)
        {
            _nextButton.onClick.RemoveListener(OnNextClicked);
            _nextButton.onClick.AddListener(OnNextClicked);
        }
    }

    private void Awake()
    {
        if (_root == null)
        {
            _root = gameObject;
        }

        EnsureLoopVideoPlayback();
        ResetSceneVideoPlayerOnLoad();
        ApplyBodyTextSettings();
        SetAllStepIllustrationImagesEnabled(false);

        if (_nextButton != null)
        {
            _nextButton.onClick.AddListener(OnNextClicked);
        }

        HideImmediate();
    }

    private void OnDestroy()
    {
        if (_nextButton != null)
        {
            _nextButton.onClick.RemoveListener(OnNextClicked);
        }

        ReleaseLoopVideoResources();
    }

    public void HideImmediate()
    {
        StopLoopVideo();
        HideIllustrationImage();
        HideLoopVideoImage();
        if (_root != null)
        {
            _root.SetActive(false);
        }
    }

    public async UniTask ShowStepAsync(BattleTutorialStepSO step, bool isLastStep, CancellationToken token)
    {
        if (step == null)
        {
            return;
        }

        PrepareForDisplay();
        EnsureLoopVideoPlayback();

        if (_titleText != null)
        {
            _titleText.text = step.Title ?? string.Empty;
        }

        if (_bodyText != null)
        {
            _bodyText.text = FormatBodyWithLineBreaks(step.Body);
        }

        if (_nextButtonLabel != null)
        {
            _nextButtonLabel.text = "次へ";
        }

        if (_root != null)
        {
            _root.SetActive(true);
        }

        RestoreNextButtonInteractable();
        await ApplyIllustrationAsync(step, token);

        _advanceTcs = new UniTaskCompletionSource();
        CancellationToken popupDestroyToken = this.GetCancellationTokenOnDestroy();
        using (popupDestroyToken.Register(() => _advanceTcs?.TrySetCanceled()))
        {
            try
            {
                await _advanceTcs.Task;
            }
            catch (OperationCanceledException) when (!popupDestroyToken.IsCancellationRequested)
            {
                // バトル側トークンなど外部要因のキャンセルは閉じるだけ扱いにする。
            }
        }

        HideImmediate();
    }

    private static string FormatBodyWithLineBreaks(string body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }

        return body.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    private void ApplyBodyTextSettings()
    {
        if (_bodyText == null)
        {
            return;
        }

        _bodyText.textWrappingMode = TextWrappingModes.Normal;
        _bodyText.overflowMode = TextOverflowModes.Overflow;
        _bodyText.richText = false;
        _bodyText.alignment = TextAlignmentOptions.TopLeft;
    }

    private async UniTask ApplyIllustrationAsync(BattleTutorialStepSO step, CancellationToken token)
    {
        switch (step.IllustrationKind)
        {
            case BattleTutorialIllustrationKind.Video:
                HideIllustrationImage();
                VideoClip loopVideo = step.LoopVideo;
                if (loopVideo != null)
                {
                    await PlayLoopVideoAsync(loopVideo, token);
                }
                else
                {
                    StopLoopVideo();
                    HideLoopVideoImage();
                    Debug.LogWarning(
                        $"[BattleTutorialPopupView] Illustration Kind=Video ですが Loop Video が未設定です。"
                        + $" step={step.name}（InGame > Setup Tutorial Assets で Movie_005 を割り当ててください）",
                        this);
                }

                break;

            case BattleTutorialIllustrationKind.Illustration:
                StopLoopVideo();
                HideLoopVideoImage();
                ShowStepIllustrationImage(step);
                break;

            default:
                StopLoopVideo();
                HideIllustrationImage();
                HideLoopVideoImage();
                break;
        }
    }

    private void HideIllustrationImage()
    {
        SetAllStepIllustrationImagesEnabled(false);
    }

    private void ShowStepIllustrationImage(BattleTutorialStepSO step)
    {
        SetAllStepIllustrationImagesEnabled(false);
        Image image = ResolveIllustrationImage(step);
        if (image == null)
        {
            Debug.LogWarning(
                $"[BattleTutorialPopupView] Illustration 用 Image が未設定です。"
                + $" stepId={step.StepId} step={step.name}"
                + "（BattleTutorialPopupView の Step Illustration Bindings を確認してください）",
                this);
            return;
        }

        if (image.sprite == null)
        {
            Debug.LogWarning(
                $"[BattleTutorialPopupView] Illustration Image に Sprite が未設定です: {image.name}",
                image);
        }

        image.enabled = true;
    }

    private Image ResolveIllustrationImage(BattleTutorialStepSO step)
    {
        if (step == null || string.IsNullOrEmpty(step.StepId) || _stepIllustrationBindings == null)
        {
            return null;
        }

        for (int i = 0; i < _stepIllustrationBindings.Length; i++)
        {
            BattleTutorialStepIllustrationBinding binding = _stepIllustrationBindings[i];
            if (binding == null || binding.Image == null)
            {
                continue;
            }

            if (binding.StepId == step.StepId)
            {
                return binding.Image;
            }
        }

        return null;
    }

    private void SetAllStepIllustrationImagesEnabled(bool enabled)
    {
        if (_stepIllustrationBindings == null)
        {
            return;
        }

        for (int i = 0; i < _stepIllustrationBindings.Length; i++)
        {
            Image image = _stepIllustrationBindings[i]?.Image;
            if (image != null)
            {
                image.enabled = enabled;
            }
        }
    }

    private RectTransform ResolveIllustrationMediaHost()
    {
        if (_illustrationMediaHost != null)
        {
            return _illustrationMediaHost;
        }

        if (_loopVideoImage != null)
        {
            return _loopVideoImage.rectTransform.parent as RectTransform;
        }

        if (_stepIllustrationBindings != null)
        {
            for (int i = 0; i < _stepIllustrationBindings.Length; i++)
            {
                Image image = _stepIllustrationBindings[i]?.Image;
                if (image != null)
                {
                    return image.rectTransform.parent as RectTransform;
                }
            }
        }

        return transform as RectTransform;
    }

    private void HideLoopVideoImage()
    {
        if (_loopVideoImage != null)
        {
            _loopVideoImage.enabled = false;
        }
    }

    private void EnsureLoopVideoPlayback()
    {
        if (_loopVideoPlayer != null && _loopVideoImage != null)
        {
            ConfigureVideoPlayerDefaults();
            return;
        }

        Transform host = ResolveIllustrationMediaHost();
        Transform videoRoot = host != null ? host.Find("Video") : null;
        if (videoRoot == null && host != null)
        {
            videoRoot = host.Find("LoopVideo");
        }
        if (videoRoot == null)
        {
            var videoGo = new GameObject(
                "LoopVideo",
                typeof(RectTransform),
                typeof(RawImage),
                typeof(VideoPlayer));
            videoRoot = videoGo.transform;
            videoRoot.SetParent(host, false);
            RectTransform videoRect = videoGo.GetComponent<RectTransform>();
            videoRect.anchorMin = Vector2.zero;
            videoRect.anchorMax = Vector2.one;
            videoRect.offsetMin = Vector2.zero;
            videoRect.offsetMax = Vector2.zero;
        }

        if (_loopVideoImage == null)
        {
            _loopVideoImage = videoRoot.GetComponent<RawImage>();
            _loopVideoImage.color = Color.white;
            _loopVideoImage.raycastTarget = false;
        }

        if (_loopVideoPlayer == null)
        {
            _loopVideoPlayer = videoRoot.GetComponent<VideoPlayer>();
        }

        ConfigureVideoPlayerDefaults();
        _loopVideoImage.enabled = false;
    }

    private void ConfigureVideoPlayerDefaults()
    {
        if (_loopVideoPlayer == null)
        {
            return;
        }

        _loopVideoPlayer.playOnAwake = false;
        _loopVideoPlayer.isLooping = true;
        _loopVideoPlayer.waitForFirstFrame = false;
        _loopVideoPlayer.skipOnDrop = true;
        _loopVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _loopVideoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        _loopVideoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
    }

    private void ResetSceneVideoPlayerOnLoad()
    {
        if (_loopVideoPlayer == null)
        {
            return;
        }

        ConfigureVideoPlayerDefaults();
        _loopVideoPlayer.Stop();
        _loopVideoPlayer.clip = null;
    }

    private async UniTask PlayLoopVideoAsync(VideoClip clip, CancellationToken token)
    {
        if (clip == null || _loopVideoPlayer == null || _loopVideoImage == null)
        {
            return;
        }

        if (!await PrepareLoopVideoAsync(clip, token))
        {
            return;
        }

        RenderTexture renderTexture = ResolveRenderTexture(clip);
        if (renderTexture != null)
        {
            _loopVideoImage.texture = renderTexture;
            _loopVideoImage.enabled = true;
        }

        StartPreparedPlayback();
    }

    private async UniTask<bool> PrepareLoopVideoAsync(VideoClip clip, CancellationToken token)
    {
        if (clip == null || _loopVideoPlayer == null)
        {
            return false;
        }

        EnsureLoopVideoPlayback();
        ConfigureVideoPlayerDefaults();

        if (_preparedClip == clip && _loopVideoPlayer.clip == clip && _loopVideoPlayer.isPrepared)
        {
            return true;
        }

        RenderTexture renderTexture = ResolveRenderTexture(clip);
        if (renderTexture == null)
        {
            Debug.LogWarning(
                $"[BattleTutorialPopupView] RenderTexture を解決できませんでした: {clip.name}",
                this);
            return false;
        }

        if (_root != null && _root.activeSelf)
        {
            return await PrepareLoopVideoCoreAsync(clip, renderTexture, token);
        }

        bool prepared = false;
        await RunWithHiddenRootAsync(
            async () => prepared = await PrepareLoopVideoCoreAsync(clip, renderTexture, token),
            token);
        return prepared;
    }

    private async UniTask<bool> PrepareLoopVideoCoreAsync(
        VideoClip clip,
        RenderTexture renderTexture,
        CancellationToken token)
    {
        EnsureRenderTextureReady(renderTexture);

        if (!_loopVideoPlayer.gameObject.activeInHierarchy)
        {
            _loopVideoPlayer.gameObject.SetActive(true);
        }

        _loopVideoPlayer.Stop();
        _loopVideoPlayer.clip = null;
        await UniTask.Yield(PlayerLoopTiming.Update, token);

        _loopVideoPlayer.targetTexture = renderTexture;
        _loopVideoPlayer.isLooping = true;
        _loopVideoPlayer.clip = clip;

        if (!await WaitForVideoPreparedAsync(_loopVideoPlayer, token))
        {
            return false;
        }

        _preparedClip = clip;
        return true;
    }

    private static async UniTask<bool> WaitForVideoPreparedAsync(VideoPlayer player, CancellationToken token)
    {
        if (player == null)
        {
            return false;
        }

        player.Prepare();

        if (player.isPrepared)
        {
            return true;
        }

        const float maxWaitSeconds = 30f;
        float elapsed = 0f;
        while (!player.isPrepared && elapsed < maxWaitSeconds)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            elapsed += Time.unscaledDeltaTime;
        }

        if (!player.isPrepared)
        {
            Debug.LogWarning(
                $"[BattleTutorialPopupView] 動画の Prepare に失敗しました: {player.clip?.name ?? "(null)"}",
                player);
            return false;
        }

        return true;
    }

    private void StartPreparedPlayback()
    {
        if (_loopVideoPlayer == null)
        {
            return;
        }

        _loopVideoPlayer.time = 0;
        _loopVideoPlayer.Play();
    }

    private async UniTask RunWithHiddenRootAsync(System.Func<UniTask> action, CancellationToken token)
    {
        if (_root == null)
        {
            await action();
            return;
        }

        bool wasActive = _root.activeSelf;
        CanvasGroup group = _root.GetComponent<CanvasGroup>();
        bool addedGroup = false;
        float previousAlpha = 1f;
        bool previousBlocksRaycasts = true;

        if (group == null)
        {
            group = _root.AddComponent<CanvasGroup>();
            addedGroup = true;
        }
        else
        {
            previousAlpha = group.alpha;
            previousBlocksRaycasts = group.blocksRaycasts;
        }

        // interactable は触らない（子の Button が無効色になるのを防ぐ）
        group.alpha = 0f;
        group.blocksRaycasts = false;
        _root.SetActive(true);

        try
        {
            await action();
        }
        finally
        {
            if (!wasActive)
            {
                _root.SetActive(false);
            }

            if (addedGroup)
            {
                Destroy(group);
            }
            else
            {
                group.alpha = previousAlpha;
                group.blocksRaycasts = previousBlocksRaycasts;
            }
        }
    }

    private void RestoreNextButtonInteractable()
    {
        if (_nextButton == null)
        {
            return;
        }

        _nextButton.interactable = true;
    }

    private RenderTexture ResolveRenderTexture(VideoClip clip)
    {
        if (_sharedVideoRenderTexture != null)
        {
            ReleaseOwnedRenderTexture();
            _loopVideoRenderTexture = _sharedVideoRenderTexture;
            _ownsRenderTexture = false;
            return _loopVideoRenderTexture;
        }

        RenderTexture sceneTexture = null;
        if (_loopVideoImage != null && _loopVideoImage.texture is RenderTexture imageTexture)
        {
            sceneTexture = imageTexture;
        }
        else if (_loopVideoPlayer != null && _loopVideoPlayer.targetTexture is RenderTexture playerTexture)
        {
            sceneTexture = playerTexture;
        }

        if (sceneTexture != null)
        {
            ReleaseOwnedRenderTexture();
            _loopVideoRenderTexture = sceneTexture;
            _ownsRenderTexture = false;
            return sceneTexture;
        }

        int width = clip.width > 0 ? (int)clip.width : 1920;
        int height = clip.height > 0 ? (int)clip.height : 1080;
        EnsureDynamicRenderTexture(width, height);
        return _loopVideoRenderTexture;
    }

    private static void EnsureRenderTextureReady(RenderTexture renderTexture)
    {
        if (renderTexture == null)
        {
            return;
        }

        if (!renderTexture.IsCreated())
        {
            renderTexture.Create();
        }
    }

    private void EnsureDynamicRenderTexture(int width, int height)
    {
        if (_loopVideoRenderTexture != null
            && _ownsRenderTexture
            && _loopVideoRenderTexture.width == width
            && _loopVideoRenderTexture.height == height)
        {
            EnsureRenderTextureReady(_loopVideoRenderTexture);
            return;
        }

        ReleaseOwnedRenderTexture();
        _loopVideoRenderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        _loopVideoRenderTexture.Create();
        _ownsRenderTexture = true;
    }

    private void StopLoopVideo()
    {
        if (_loopVideoPlayer == null)
        {
            return;
        }

        if (_loopVideoPlayer.isPlaying)
        {
            _loopVideoPlayer.Stop();
        }

        _loopVideoPlayer.clip = null;
        _preparedClip = null;
    }

    private void ReleaseLoopVideoResources()
    {
        StopLoopVideo();
        ReleaseOwnedRenderTexture();
    }

    private void ReleaseOwnedRenderTexture()
    {
        if (!_ownsRenderTexture || _loopVideoRenderTexture == null)
        {
            return;
        }

        if (_loopVideoPlayer != null && _loopVideoPlayer.targetTexture == _loopVideoRenderTexture)
        {
            _loopVideoPlayer.targetTexture = null;
        }

        if (_loopVideoImage != null && _loopVideoImage.texture == _loopVideoRenderTexture)
        {
            _loopVideoImage.texture = null;
        }

        _loopVideoRenderTexture.Release();
        Destroy(_loopVideoRenderTexture);
        _loopVideoRenderTexture = null;
        _ownsRenderTexture = false;
    }

    private void OnNextClicked()
    {
        InGameSe.Play(InGameSeKey.UiTutorialNext);
        _advanceTcs?.TrySetResult();
    }

    private void PrepareForDisplay()
    {
        ReparentToMainCanvasIfNeeded();
        if (transform is RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        Canvas hostCanvas = GetComponentInParent<Canvas>(true)?.rootCanvas;
        BattleTutorialUiFactory.EnsureHostCanvasVisible(hostCanvas);

        Canvas popupCanvas = GetComponent<Canvas>();
        if (popupCanvas == null)
        {
            popupCanvas = gameObject.AddComponent<Canvas>();
        }

        popupCanvas.overrideSorting = true;
        popupCanvas.sortingOrder = 500;

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        transform.SetAsLastSibling();
    }

    private void ReparentToMainCanvasIfNeeded()
    {
        GameObject mainCanvasGo = GameObject.Find("MainCanvas");
        if (mainCanvasGo == null)
        {
            return;
        }

        Transform host = mainCanvasGo.transform;
        if (transform.parent == host)
        {
            return;
        }

        transform.SetParent(host, false);
    }
}
