using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// チュートリアルポップアップ UI の構築（Editor セットアップとランタイムフォールバック共用）。
/// </summary>
public static class BattleTutorialUiFactory
{
    const string MainCanvasName = "MainCanvas";
    const string PopupObjectName = "BattleTutorialPopup";
    const string OpeningOverlayName = "BattleTutorialOpening";
    const string CommandFocusOverlayName = "BattleTutorialCommandFocus";

    /// <summary>バトル HUD と同じ MainCanvas を優先して返す。</summary>
    public static Canvas ResolveHostCanvas()
    {
        GameObject mainCanvasGo = GameObject.Find(MainCanvasName);
        if (mainCanvasGo != null && mainCanvasGo.TryGetComponent(out Canvas mainCanvas))
        {
            return mainCanvas;
        }

        CommandPanelView commandPanel = Object.FindFirstObjectByType<CommandPanelView>();
        if (commandPanel != null)
        {
            Canvas fromHud = commandPanel.GetComponentInParent<Canvas>(true);
            if (fromHud != null)
            {
                return fromHud.rootCanvas;
            }
        }

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.name.Contains("Result"))
            {
                continue;
            }

            if (canvas.transform is RectTransform rect && rect.localScale.sqrMagnitude > 0.01f)
            {
                return canvas.rootCanvas;
            }
        }

        return canvases.Length > 0 ? canvases[0].rootCanvas : null;
    }

    public static void EnsureHostCanvasVisible(Canvas canvas)
    {
        if (canvas == null || canvas.transform is not RectTransform canvasRect)
        {
            return;
        }

        if (canvasRect.localScale.sqrMagnitude < 0.01f)
        {
            canvasRect.localScale = Vector3.one;
        }
    }

    public static BattleTutorialPopupView CreatePopupUnderCanvas(Canvas canvas)
    {
        canvas = canvas != null ? canvas.rootCanvas : ResolveHostCanvas();
        if (canvas == null)
        {
            return null;
        }

        EnsureHostCanvasVisible(canvas);

        Transform existing = canvas.transform.Find(PopupObjectName);
        if (existing != null && existing.TryGetComponent(out BattleTutorialPopupView existingView))
        {
            return existingView;
        }

        var root = new GameObject(
            PopupObjectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(BattleTutorialPopupView));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image dim = root.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.65f);
        dim.raycastTarget = true;

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(900f, 620f);
        panelRect.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.95f);

        TextMeshProUGUI titleText = CreateTmpText(panel.transform, "TitleText", 36, FontStyles.Bold);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0.05f, 0.82f);
        titleRect.anchorMax = new Vector2(0.95f, 0.95f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        var illustrationArea = new GameObject("IllustrationArea", typeof(RectTransform));
        illustrationArea.transform.SetParent(panel.transform, false);
        RectTransform illustrationAreaRect = illustrationArea.GetComponent<RectTransform>();
        illustrationAreaRect.anchorMin = new Vector2(0.05f, 0.42f);
        illustrationAreaRect.anchorMax = new Vector2(0.95f, 0.8f);
        illustrationAreaRect.offsetMin = Vector2.zero;
        illustrationAreaRect.offsetMax = Vector2.zero;

        var videoGo = new GameObject(
            "LoopVideo",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage),
            typeof(VideoPlayer));
        videoGo.transform.SetParent(illustrationArea.transform, false);
        RectTransform videoRect = videoGo.GetComponent<RectTransform>();
        videoRect.anchorMin = Vector2.zero;
        videoRect.anchorMax = Vector2.one;
        videoRect.offsetMin = Vector2.zero;
        videoRect.offsetMax = Vector2.zero;
        RawImage loopVideoImage = videoGo.GetComponent<RawImage>();
        loopVideoImage.color = Color.white;
        loopVideoImage.raycastTarget = false;
        loopVideoImage.enabled = false;
        VideoPlayer loopVideoPlayer = videoGo.GetComponent<VideoPlayer>();
        loopVideoPlayer.playOnAwake = false;
        loopVideoPlayer.isLooping = true;
        loopVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
        loopVideoPlayer.audioOutputMode = VideoAudioOutputMode.None;

        TextMeshProUGUI bodyText = CreateBodyText(panel.transform, "BodyText", 26);

        var buttonGo = new GameObject("NextButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(panel.transform, false);
        RectTransform buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.65f, 0.04f);
        buttonRect.anchorMax = new Vector2(0.95f, 0.14f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
        Button nextButton = buttonGo.GetComponent<Button>();

        TextMeshProUGUI buttonLabel = CreateTmpText(buttonGo.transform, "Label", 28, FontStyles.Bold);
        RectTransform labelRect = buttonLabel.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        buttonLabel.alignment = TextAlignmentOptions.Center;

        BattleTutorialPopupView view = root.GetComponent<BattleTutorialPopupView>();
        view.Configure(
            root,
            titleText,
            bodyText,
            nextButton,
            buttonLabel,
            loopVideoImage,
            loopVideoPlayer,
            illustrationAreaRect);
        root.SetActive(false);
        return view;
    }

    public static BattleTutorialOpeningView CreateOpeningOverlay(Canvas canvas)
    {
        canvas = canvas != null ? canvas.rootCanvas : ResolveHostCanvas();
        if (canvas == null)
        {
            return null;
        }

        EnsureHostCanvasVisible(canvas);

        Transform existing = canvas.transform.Find(OpeningOverlayName);
        if (existing != null && existing.TryGetComponent(out BattleTutorialOpeningView existingView))
        {
            existingView.PrepareForShow();
            existing.SetAsLastSibling();
            return existingView;
        }

        var root = new GameObject(
            OpeningOverlayName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup),
            typeof(BattleTutorialOpeningView));
        root.transform.SetParent(canvas.transform, false);
        root.transform.SetAsLastSibling();
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image blackPanel = root.GetComponent<Image>();
        blackPanel.color = Color.black;
        blackPanel.raycastTarget = true;

        CanvasGroup rootGroup = root.GetComponent<CanvasGroup>();
        rootGroup.alpha = 1f;
        rootGroup.blocksRaycasts = true;
        rootGroup.interactable = false;

        TextMeshProUGUI bodyLabel = CreateOpeningBodyText(root.transform, "BodyLabel", 30f);
        RectTransform bodyRect = bodyLabel.rectTransform;
        bodyRect.anchorMin = new Vector2(0.08f, 0.32f);
        bodyRect.anchorMax = new Vector2(0.92f, 0.52f);
        bodyRect.offsetMin = Vector2.zero;
        bodyRect.offsetMax = Vector2.zero;
        bodyLabel.alignment = TextAlignmentOptions.Center;
        bodyLabel.color = new Color(0.92f, 0.92f, 0.92f, 0f);

        BattleTutorialOpeningView view = root.GetComponent<BattleTutorialOpeningView>();
        view.Configure(root, rootGroup, blackPanel, bodyLabel);
        view.PrepareForShow();
        return view;
    }

    public static BattleTutorialCommandFocusOverlay CreateCommandFocusOverlay(Canvas canvas)
    {
        canvas = canvas != null ? canvas.rootCanvas : ResolveHostCanvas();
        if (canvas == null)
        {
            return null;
        }

        EnsureHostCanvasVisible(canvas);

        Transform existing = canvas.transform.Find(CommandFocusOverlayName);
        if (existing != null && existing.TryGetComponent(out BattleTutorialCommandFocusOverlay existingOverlay))
        {
            return existingOverlay;
        }

        var root = new GameObject(
            CommandFocusOverlayName,
            typeof(RectTransform),
            typeof(BattleTutorialCommandFocusOverlay));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        RectTransform top = CreateDimPanel(root.transform, "Top");
        RectTransform left = CreateDimPanel(root.transform, "Left");
        RectTransform right = CreateDimPanel(root.transform, "Right");
        RectTransform bottom = CreateDimPanel(root.transform, "Bottom");

        BattleTutorialCommandFocusOverlay overlay = root.GetComponent<BattleTutorialCommandFocusOverlay>();
        overlay.Configure(rootRect, top, left, right, bottom);
        root.SetActive(false);
        return overlay;
    }

    static RectTransform CreateDimPanel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.65f);
        image.raycastTarget = true;
        return go.GetComponent<RectTransform>();
    }

    static TextMeshProUGUI CreateBodyText(Transform parent, string name, float fontSize)
    {
        TextMeshProUGUI bodyText = CreateTmpText(parent, name, fontSize, FontStyles.Normal);
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.overflowMode = TextOverflowModes.Overflow;
        bodyText.richText = false;
        return bodyText;
    }

    static TextMeshProUGUI CreateTmpText(Transform parent, string name, float fontSize, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    static TextMeshProUGUI CreateOpeningBodyText(Transform parent, string name, float fontSize)
    {
        TextMeshProUGUI body = CreateTmpText(parent, name, fontSize, FontStyles.Normal);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.overflowMode = TextOverflowModes.Overflow;
        body.richText = false;
        return body;
    }
}
