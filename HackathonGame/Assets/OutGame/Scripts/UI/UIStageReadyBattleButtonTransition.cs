using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Swaps ToReadyButton for ToBattleButton, and reveals ToOutfitButton with the same emerge animation.
/// </summary>
[DisallowMultipleComponent]
public class UIStageReadyBattleButtonTransition : MonoBehaviour
{
    [SerializeField] Button toReadyButton;
    [SerializeField] Button toBattleButton;
    [SerializeField] Button toOutfitButton;
    [SerializeField] Button backToStageButton;
    [SerializeField] float swapDuration = 0.38f;
    [SerializeField] [Range(1f, 6f)] float swapEaseOutPower = 3f;
    [SerializeField] float swapSlideOffsetX = 48f;
    [SerializeField] float swapScaleFrom = 0.9f;
    [SerializeField] UIStageReadyBannerFlyIn readyBannerFlyIn;
    [SerializeField] UIStageReadyBannerFlyIn backToStageFlyIn;
    [SerializeField] StageScanWaveAnimator stageScanWaveAnimator;
    [SerializeField] UITurntableDragRotator cdTurntableRotator;
    [SerializeField] UITurntableDrivenVerticalScroll noisesVerticalScroll;
    [SerializeField] StageNoiseSlotFocus noisesSlotFocus;

    bool _swapCompleted;
    bool _swapInProgress;
    Coroutine _swapCoroutine;
    string _readyBattleNoiseChildName;

    public bool SwapCompleted => _swapCompleted;

    public void Configure(
        Button readyButton,
        Button battleButton,
        Button outfitButton,
        Button backToStage,
        float duration,
        float easeOutPower,
        float slideOffsetX,
        float scaleFrom,
        UIStageReadyBannerFlyIn bannerFlyIn,
        UIStageReadyBannerFlyIn backToStageBannerFlyIn,
        StageScanWaveAnimator scanWaveAnimator,
        UITurntableDragRotator cdTurntable,
        UITurntableDrivenVerticalScroll noisesScroll,
        StageNoiseSlotFocus slotFocus)
    {
        toReadyButton = readyButton;
        toBattleButton = battleButton;
        if (outfitButton != null)
        {
            toOutfitButton = outfitButton;
        }

        if (backToStage != null)
        {
            backToStageButton = backToStage;
        }

        swapDuration = duration;
        swapEaseOutPower = easeOutPower;
        swapSlideOffsetX = slideOffsetX;
        swapScaleFrom = scaleFrom;
        readyBannerFlyIn = bannerFlyIn;
        backToStageFlyIn = backToStageBannerFlyIn;
        stageScanWaveAnimator = scanWaveAnimator;
        cdTurntableRotator = cdTurntable;
        noisesVerticalScroll = noisesScroll;
        noisesSlotFocus = slotFocus;
    }

    public void EnsureButtonReferences()
    {
        EnsureButtonReferences(prepareOutfitHidden: true);
    }

    void EnsureButtonReferences(bool prepareOutfitHidden)
    {
        Transform searchRoot = ResolveCanvasRoot();

        if (toReadyButton == null)
        {
            toReadyButton = FindAndSetupImageButton(searchRoot, "ToReadyButton");
        }

        if (toBattleButton == null)
        {
            toBattleButton = FindAndSetupImageButton(searchRoot, "ToBattleButton");
        }

        if (toOutfitButton == null)
        {
            toOutfitButton = FindAndSetupImageButton(searchRoot, "ToOutfitButton");
        }

        if (backToStageButton == null)
        {
            backToStageButton = FindAndSetupImageButton(searchRoot, "BackToStageButton");
        }

        if (prepareOutfitHidden)
        {
            PrepareOutfitButtonHidden();
        }
    }

    void PrepareOutfitButtonHidden()
    {
        if (toOutfitButton == null)
        {
            return;
        }

        toOutfitButton.gameObject.SetActive(false);
    }

    public void BindReadyButtonClick()
    {
        EnsureButtonReferences();

        if (toReadyButton == null)
        {
            return;
        }

        toReadyButton.onClick.RemoveListener(OnToReadyClicked);
        toReadyButton.onClick.AddListener(OnToReadyClicked);
    }

    public void BindBackToStageButtonClick()
    {
        EnsureButtonReferences();

        if (backToStageButton == null)
        {
            return;
        }

        backToStageButton.onClick.RemoveListener(OnBackToStageClicked);
        backToStageButton.onClick.AddListener(OnBackToStageClicked);
    }

    public void BindOutfitButtonClick()
    {
        EnsureButtonReferences();

        if (toOutfitButton == null)
        {
            return;
        }

        toOutfitButton.onClick.RemoveListener(OnToOutfitClicked);
        toOutfitButton.onClick.AddListener(OnToOutfitClicked);
    }

    public void BindBattleButtonClick()
    {
        EnsureButtonReferences();

        if (toBattleButton == null)
        {
            return;
        }

        toBattleButton.onClick.RemoveListener(OnToBattleClicked);
        toBattleButton.onClick.AddListener(OnToBattleClicked);
    }

    void OnDestroy()
    {
        if (toReadyButton != null)
        {
            toReadyButton.onClick.RemoveListener(OnToReadyClicked);
        }

        if (backToStageButton != null)
        {
            backToStageButton.onClick.RemoveListener(OnBackToStageClicked);
        }

        if (toOutfitButton != null)
        {
            toOutfitButton.onClick.RemoveListener(OnToOutfitClicked);
        }

        if (toBattleButton != null)
        {
            toBattleButton.onClick.RemoveListener(OnToBattleClicked);
        }
    }

    void OnToReadyClicked()
    {
        if (_swapCompleted || _swapInProgress)
        {
            return;
        }

        if (_swapCoroutine != null)
        {
            StopCoroutine(_swapCoroutine);
        }

        _swapCoroutine = StartCoroutine(HandleToReadyClick());
    }

    void OnBackToStageClicked()
    {
        if (!_swapCompleted || _swapInProgress)
        {
            return;
        }

        if (_swapCoroutine != null)
        {
            StopCoroutine(_swapCoroutine);
        }

        _swapCoroutine = StartCoroutine(HandleBackToStageClick());
    }

    void OnToOutfitClicked()
    {
        if (!_swapCompleted || _swapInProgress || _outfitNavInProgress)
        {
            return;
        }

        StartCoroutine(HandleToOutfitClick());
    }

    bool _outfitNavInProgress;
    bool _battleNavInProgress;

    void OnToBattleClicked()
    {
        if (!_swapCompleted || _swapInProgress || _battleNavInProgress)
        {
            return;
        }

        StartCoroutine(HandleToBattleClick());
    }

    IEnumerator HandleToBattleClick()
    {
        _battleNavInProgress = true;

        if (toBattleButton != null)
        {
            toBattleButton.interactable = false;
        }

        UIButtonPressFeedback pressFeedback = toBattleButton != null
            ? toBattleButton.GetComponent<UIButtonPressFeedback>()
            : null;

        if (pressFeedback != null)
        {
            yield return pressFeedback.PlayClickConfirm();
        }

        CommitBattleStageFromFocusedNoise();
        SceneTransferManager.Instance.LoadNewScene(SceneNames.Main);
        _battleNavInProgress = false;
    }

    void CommitBattleStageFromFocusedNoise()
    {
        EnsureNoiseSlotFocusReference();

        string noiseChildName = ResolveBattleNoiseChildName();
        string statusKey = !string.IsNullOrEmpty(noiseChildName)
            ? StageStatusFocusSync.ResolveStatusKeyFromNoiseName(noiseChildName)
            : null;

        if (StageBattleStageIds.TryGetStageIdForStatusKey(statusKey, out string stageId))
        {
            BattleStageSession.SetPendingStageId(stageId, noiseChildName);
            return;
        }

        BattleStageSession.SetPendingStageId(StageBattleStageIds.NormalStage, noiseChildName);

        if (!string.IsNullOrEmpty(statusKey) && statusKey != "Normal")
        {
            Debug.LogWarning(
                $"[UIStageReadyBattleButtonTransition] '{statusKey}' は戦闘ステージ未接続のため MainScene の Fallback を使用します（撃破記録は '{noiseChildName}'）。",
                this);
        }
    }

    void CaptureReadyBattleNoiseCommit()
    {
        EnsureNoiseSlotFocusReference();
        string noiseChildName = ResolveBattleNoiseChildName();
        if (string.IsNullOrEmpty(noiseChildName))
        {
            return;
        }

        _readyBattleNoiseChildName = noiseChildName;
        BattleStageSession.CommitBattleNoiseChildName(noiseChildName);
    }

    void ClearReadyBattleNoiseCommit()
    {
        _readyBattleNoiseChildName = null;
    }

    void EnsureNoiseSlotFocusReference()
    {
        if (noisesSlotFocus != null)
        {
            return;
        }

        noisesSlotFocus = GetComponent<StageNoiseSlotFocus>();
        if (noisesSlotFocus == null)
        {
            noisesSlotFocus = FindAnyObjectByType<StageNoiseSlotFocus>();
        }
    }

    string ResolveBattleNoiseChildName()
    {
        // ToReady 時にロックした名前を最優先（Ready 中のフォーカスずれ・先頭 Normal フォールバックを防ぐ）
        if (!string.IsNullOrEmpty(_readyBattleNoiseChildName))
        {
            return _readyBattleNoiseChildName;
        }

        if (!string.IsNullOrEmpty(BattleStageSession.CommittedBattleNoiseChildName))
        {
            return BattleStageSession.CommittedBattleNoiseChildName;
        }

        if (noisesSlotFocus != null && noisesSlotFocus.FocusedChild != null)
        {
            return noisesSlotFocus.FocusedChild.name;
        }

        if (noisesSlotFocus != null)
        {
            RectTransform fallbackChild = noisesSlotFocus.FindFirstActiveNoiseChild();
            if (fallbackChild != null)
            {
                return fallbackChild.name;
            }
        }

        return null;
    }

    IEnumerator HandleToOutfitClick()
    {
        _outfitNavInProgress = true;

        if (toOutfitButton != null)
        {
            toOutfitButton.interactable = false;
        }

        UIButtonPressFeedback pressFeedback = toOutfitButton != null
            ? toOutfitButton.GetComponent<UIButtonPressFeedback>()
            : null;

        if (pressFeedback != null)
        {
            yield return pressFeedback.PlayClickConfirm();
        }

        if (noisesSlotFocus == null)
        {
            noisesSlotFocus = GetComponent<StageNoiseSlotFocus>();
        }

        string focusedNoiseName = null;
        string statusKey = null;
        if (noisesSlotFocus != null && noisesSlotFocus.FocusedChild != null)
        {
            focusedNoiseName = noisesSlotFocus.FocusedChild.name;
            statusKey = StageStatusFocusSync.ResolveStatusKeyFromNoiseName(focusedNoiseName);
        }

        OutfitSceneReturnContext.MarkFromStageReady(statusKey, focusedNoiseName);
        SceneTransferManager.Instance.LoadNewScene(SceneNames.Outfit);
        _outfitNavInProgress = false;
    }

    public void RestoreReadyBattleNoiseCommit(string focusedNoiseName)
    {
        if (!string.IsNullOrEmpty(focusedNoiseName))
        {
            _readyBattleNoiseChildName = focusedNoiseName;
            BattleStageSession.CommitBattleNoiseChildName(focusedNoiseName);
            return;
        }

        CaptureReadyBattleNoiseCommit();
    }

    public void RestoreReadyStateAfterOutfit()
    {
        EnsureButtonReferences(prepareOutfitHidden: false);
        EnsureReadyBannerFlyInReference();
        EnsureStageScanWaveAnimatorReference();

        if (toReadyButton != null)
        {
            toReadyButton.gameObject.SetActive(false);
        }

        if (toBattleButton != null)
        {
            toBattleButton.gameObject.SetActive(true);
            RestoreButtonVisualAtRest(toBattleButton);
            toBattleButton.interactable = true;
        }

        if (toOutfitButton != null)
        {
            toOutfitButton.gameObject.SetActive(true);
            RestoreButtonVisualAtRest(toOutfitButton);
            toOutfitButton.interactable = true;
        }

        if (stageScanWaveAnimator != null)
        {
            stageScanWaveAnimator.RestorePostScanAppearance();
        }

        if (readyBannerFlyIn != null)
        {
            readyBannerFlyIn.ShowAtRest();
        }

        ShowBackToStageButton();

        SetStageSelectionInputEnabled(false);
        _swapCompleted = true;
        _swapInProgress = false;
    }

    void ShowBackToStageButton(bool interactable = true)
    {
        EnsureButtonReferences(prepareOutfitHidden: false);

        if (backToStageButton == null)
        {
            return;
        }

        backToStageButton.gameObject.SetActive(true);
        backToStageButton.interactable = interactable;
        BringBackToStageButtonToFront();
    }

    static void BringBackToStageButtonToFront(Button button)
    {
        if (button == null)
        {
            return;
        }

        RectTransform rect = button.transform as RectTransform;
        if (rect != null)
        {
            rect.SetAsLastSibling();
        }
    }

    void BringBackToStageButtonToFront()
    {
        BringBackToStageButtonToFront(backToStageButton);
    }

    void HideBackToStageButton()
    {
        if (backToStageButton == null)
        {
            return;
        }

        backToStageButton.gameObject.SetActive(false);
    }

    static void RestoreButtonVisualAtRest(Button button)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.targetGraphic as Image;
        if (image != null)
        {
            Color color = image.color;
            color.a = 1f;
            image.color = color;
        }

        RectTransform rect = button.transform as RectTransform;
        if (rect != null)
        {
            rect.localScale = Vector3.one;
        }
    }

    IEnumerator HandleToReadyClick()
    {
        _swapInProgress = true;
        EnsureButtonReferences();
        CaptureReadyBattleNoiseCommit();
        SetStageSelectionInputEnabled(false);

        if (toReadyButton != null)
        {
            toReadyButton.interactable = false;
        }

        UIButtonPressFeedback pressFeedback = toReadyButton != null
            ? toReadyButton.GetComponent<UIButtonPressFeedback>()
            : null;

        if (pressFeedback != null)
        {
            yield return pressFeedback.PlayClickConfirm();
        }

        EnsureReadyBannerFlyInReference();
        EnsureStageScanWaveAnimatorReference();

        Coroutine bannerFlyInCoroutine = null;
        if (readyBannerFlyIn != null)
        {
            bannerFlyInCoroutine = StartCoroutine(readyBannerFlyIn.PlayFlyIn());
        }

        ShowBackToStageButton(interactable: false);

        Coroutine scanWaveCoroutine = null;
        if (stageScanWaveAnimator != null)
        {
            scanWaveCoroutine = StartCoroutine(stageScanWaveAnimator.PlayScan());
        }

        Coroutine outfitRevealCoroutine = null;
        if (toOutfitButton != null)
        {
            outfitRevealCoroutine = StartCoroutine(PlayOutfitButtonReveal());
        }

        yield return PlaySwap();

        if (outfitRevealCoroutine != null)
        {
            yield return outfitRevealCoroutine;
        }

        if (bannerFlyInCoroutine != null)
        {
            yield return bannerFlyInCoroutine;
        }

        BringBackToStageButtonToFront();

        if (scanWaveCoroutine != null)
        {
            yield return scanWaveCoroutine;
        }

        if (backToStageButton != null)
        {
            backToStageButton.interactable = true;
        }

        _swapCompleted = true;
        _swapInProgress = false;
        _swapCoroutine = null;
    }

    IEnumerator HandleBackToStageClick()
    {
        _swapInProgress = true;
        EnsureButtonReferences(prepareOutfitHidden: false);

        if (backToStageButton != null)
        {
            backToStageButton.interactable = false;
        }

        UIButtonPressFeedback pressFeedback = backToStageButton != null
            ? backToStageButton.GetComponent<UIButtonPressFeedback>()
            : null;

        if (pressFeedback != null)
        {
            yield return pressFeedback.PlayClickConfirm();
        }

        EnsureReadyBannerFlyInReference();
        EnsureStageScanWaveAnimatorReference();

        Coroutine bannerFlyOutCoroutine = null;
        if (readyBannerFlyIn != null)
        {
            bannerFlyOutCoroutine = StartCoroutine(readyBannerFlyIn.PlayFlyOut());
        }

        HideBackToStageButton();

        Coroutine outfitHideCoroutine = null;
        if (toOutfitButton != null && toOutfitButton.gameObject.activeInHierarchy)
        {
            outfitHideCoroutine = StartCoroutine(PlayButtonHideOut(toOutfitButton));
        }

        Coroutine reverseSwapCoroutine = null;
        if (toReadyButton != null && toBattleButton != null)
        {
            reverseSwapCoroutine = StartCoroutine(PlayReverseSwap());
        }

        Coroutine scanReverseCoroutine = null;
        if (stageScanWaveAnimator != null)
        {
            scanReverseCoroutine = StartCoroutine(stageScanWaveAnimator.PlayScanReverse());
        }

        yield return WaitForCoroutines(
            bannerFlyOutCoroutine,
            outfitHideCoroutine,
            reverseSwapCoroutine,
            scanReverseCoroutine);

        SetStageSelectionInputEnabled(true);

        StageSceneReadyResume.Clear();
        ClearReadyBattleNoiseCommit();
        _swapCompleted = false;
        _swapInProgress = false;
        _swapCoroutine = null;
    }

    public IEnumerator PlaySwap()
    {
        EnsureButtonReferences(prepareOutfitHidden: false);

        if (toReadyButton == null || toBattleButton == null)
        {
            yield break;
        }

        RectTransform readyRect = toReadyButton.transform as RectTransform;
        RectTransform battleRect = toBattleButton.transform as RectTransform;
        Image readyImage = toReadyButton.targetGraphic as Image;
        Image battleImage = toBattleButton.targetGraphic as Image;

        if (readyRect == null || battleRect == null || readyImage == null || battleImage == null)
        {
            yield break;
        }

        Vector2 restPosition = readyRect.anchoredPosition;
        Vector3 readyRestScale = readyRect.localScale;
        Vector3 battleRestScale = battleRect.localScale;
        Color readyRestColor = readyImage.color;
        Color battleRestColor = battleImage.color;

        float slideOffset = Mathf.Max(0f, swapSlideOffsetX);
        float scaleFrom = Mathf.Clamp(swapScaleFrom, 0.5f, 1f);
        Vector3 shrunkScaleReady = readyRestScale * scaleFrom;
        Vector3 shrunkScaleBattle = battleRestScale * scaleFrom;

        float duration = Mathf.Max(0f, swapDuration);
        if (duration <= 0f)
        {
            readyRect.gameObject.SetActive(false);
            yield return PlayButtonRevealIn(toBattleButton);
            yield break;
        }

        toBattleButton.gameObject.SetActive(true);
        toBattleButton.interactable = false;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float eased = EvaluateEaseOut(Mathf.Clamp01(elapsed / duration));

            float readyT = Mathf.Clamp01(eased * 1.35f);
            float battleT = Mathf.Clamp01((eased - 0.22f) / 0.78f);

            readyRect.anchoredPosition = restPosition + Vector2.left * (slideOffset * readyT);
            readyRect.localScale = Vector3.LerpUnclamped(readyRestScale, shrunkScaleReady, readyT);
            readyImage.color = new Color(
                readyRestColor.r,
                readyRestColor.g,
                readyRestColor.b,
                Mathf.Lerp(readyRestColor.a, 0f, readyT));

            ApplyButtonRevealFrame(
                battleRect,
                battleImage,
                restPosition,
                battleRestScale,
                shrunkScaleBattle,
                battleRestColor,
                slideOffset,
                battleT);

            yield return null;
        }

        readyRect.gameObject.SetActive(false);
        readyRect.anchoredPosition = restPosition;
        readyRect.localScale = readyRestScale;
        readyImage.color = readyRestColor;

        battleRect.anchoredPosition = restPosition;
        battleRect.localScale = battleRestScale;
        battleImage.color = battleRestColor;
        toBattleButton.interactable = true;
    }

    /// <summary>
    /// Mirror of <see cref="PlaySwap"/>: ToBattle exits while ToReady enters at the same position.
    /// </summary>
    IEnumerator PlayReverseSwap()
    {
        EnsureButtonReferences(prepareOutfitHidden: false);

        if (toReadyButton == null || toBattleButton == null)
        {
            yield break;
        }

        RectTransform readyRect = toReadyButton.transform as RectTransform;
        RectTransform battleRect = toBattleButton.transform as RectTransform;
        Image readyImage = toReadyButton.targetGraphic as Image;
        Image battleImage = toBattleButton.targetGraphic as Image;

        if (readyRect == null || battleRect == null || readyImage == null || battleImage == null)
        {
            yield break;
        }

        Vector2 restPosition = battleRect.anchoredPosition;
        Vector3 readyRestScale = readyRect.localScale;
        Vector3 battleRestScale = battleRect.localScale;
        Color readyRestColor = readyImage.color;
        Color battleRestColor = battleImage.color;

        float slideOffset = Mathf.Max(0f, swapSlideOffsetX);
        float scaleFrom = Mathf.Clamp(swapScaleFrom, 0.5f, 1f);
        Vector3 shrunkScaleReady = readyRestScale * scaleFrom;
        Vector3 shrunkScaleBattle = battleRestScale * scaleFrom;

        float duration = Mathf.Max(0f, swapDuration);
        if (duration <= 0f)
        {
            battleRect.gameObject.SetActive(false);
            yield return PlayButtonRevealIn(toReadyButton);
            yield break;
        }

        toReadyButton.gameObject.SetActive(true);
        toReadyButton.interactable = false;
        toBattleButton.interactable = false;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float eased = EvaluateEaseOut(Mathf.Clamp01(elapsed / duration));

            float battleT = Mathf.Clamp01(eased * 1.35f);
            float readyT = Mathf.Clamp01((eased - 0.22f) / 0.78f);

            battleRect.anchoredPosition = restPosition + Vector2.left * (slideOffset * battleT);
            battleRect.localScale = Vector3.LerpUnclamped(battleRestScale, shrunkScaleBattle, battleT);
            battleImage.color = new Color(
                battleRestColor.r,
                battleRestColor.g,
                battleRestColor.b,
                Mathf.Lerp(battleRestColor.a, 0f, battleT));

            ApplyButtonRevealFrame(
                readyRect,
                readyImage,
                restPosition,
                readyRestScale,
                shrunkScaleReady,
                readyRestColor,
                slideOffset,
                readyT);

            yield return null;
        }

        battleRect.gameObject.SetActive(false);
        battleRect.anchoredPosition = restPosition;
        battleRect.localScale = battleRestScale;
        battleImage.color = battleRestColor;

        readyRect.anchoredPosition = restPosition;
        readyRect.localScale = readyRestScale;
        readyImage.color = readyRestColor;
        toReadyButton.interactable = true;
    }

    IEnumerator PlayOutfitButtonReveal()
    {
        if (toOutfitButton == null)
        {
            yield break;
        }

        yield return PlayButtonRevealIn(toOutfitButton);
    }

    IEnumerator PlayButtonRevealIn(Button button)
    {
        if (button == null)
        {
            yield break;
        }

        RectTransform rect = button.transform as RectTransform;
        Image image = button.targetGraphic as Image;
        if (rect == null || image == null)
        {
            yield break;
        }

        Vector2 restPosition = rect.anchoredPosition;
        Vector3 restScale = rect.localScale;
        Color restColor = image.color;
        float slideOffset = Mathf.Max(0f, swapSlideOffsetX);
        float scaleFrom = Mathf.Clamp(swapScaleFrom, 0.5f, 1f);
        Vector3 shrunkScale = restScale * scaleFrom;

        button.gameObject.SetActive(true);
        rect.SetAsLastSibling();
        button.interactable = false;
        ApplyButtonRevealFrame(rect, image, restPosition, restScale, shrunkScale, restColor, slideOffset, 0f);

        float duration = Mathf.Max(0f, swapDuration);
        if (duration <= 0f)
        {
            rect.anchoredPosition = restPosition;
            rect.localScale = restScale;
            image.color = restColor;
            button.interactable = true;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float eased = EvaluateEaseOut(Mathf.Clamp01(elapsed / duration));
            float revealT = Mathf.Clamp01((eased - 0.22f) / 0.78f);
            ApplyButtonRevealFrame(rect, image, restPosition, restScale, shrunkScale, restColor, slideOffset, revealT);
            yield return null;
        }

        rect.anchoredPosition = restPosition;
        rect.localScale = restScale;
        image.color = restColor;
        button.interactable = true;
    }

    IEnumerator PlayButtonHideOut(Button button)
    {
        if (button == null)
        {
            yield break;
        }

        RectTransform rect = button.transform as RectTransform;
        Image image = button.targetGraphic as Image;
        if (rect == null || image == null)
        {
            yield break;
        }

        Vector2 restPosition = rect.anchoredPosition;
        Vector3 restScale = rect.localScale;
        Color restColor = image.color;
        float slideOffset = Mathf.Max(0f, swapSlideOffsetX);
        float scaleFrom = Mathf.Clamp(swapScaleFrom, 0.5f, 1f);
        Vector3 shrunkScale = restScale * scaleFrom;

        button.interactable = false;
        ApplyButtonRevealFrame(rect, image, restPosition, restScale, shrunkScale, restColor, slideOffset, 1f);

        float duration = Mathf.Max(0f, swapDuration);
        if (duration <= 0f)
        {
            rect.anchoredPosition = restPosition + Vector2.right * slideOffset;
            rect.localScale = shrunkScale;
            image.color = new Color(restColor.r, restColor.g, restColor.b, 0f);
            button.gameObject.SetActive(false);
            rect.anchoredPosition = restPosition;
            rect.localScale = restScale;
            image.color = restColor;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float eased = EvaluateEaseOut(Mathf.Clamp01(elapsed / duration));
            float hideT = Mathf.Clamp01((eased - 0.22f) / 0.78f);
            ApplyButtonRevealFrame(rect, image, restPosition, restScale, shrunkScale, restColor, slideOffset, 1f - hideT);
            yield return null;
        }

        button.gameObject.SetActive(false);
        rect.anchoredPosition = restPosition;
        rect.localScale = restScale;
        image.color = restColor;
    }

    static void ApplyButtonRevealFrame(
        RectTransform rect,
        Image image,
        Vector2 restPosition,
        Vector3 restScale,
        Vector3 shrunkScale,
        Color restColor,
        float slideOffset,
        float revealT)
    {
        rect.anchoredPosition = restPosition + Vector2.right * (slideOffset * (1f - revealT));
        rect.localScale = Vector3.LerpUnclamped(shrunkScale, restScale, revealT);
        image.color = new Color(
            restColor.r,
            restColor.g,
            restColor.b,
            Mathf.Lerp(0f, restColor.a, revealT));
    }

    void EnsureReadyBannerFlyInReference()
    {
        if (readyBannerFlyIn != null)
        {
            return;
        }

        UIStageReadyBannerFlyIn[] flyIns = GetComponents<UIStageReadyBannerFlyIn>();
        if (flyIns.Length > 0)
        {
            readyBannerFlyIn = flyIns[0];
        }
    }

    void EnsureBackToStageFlyInReference()
    {
        if (backToStageFlyIn != null
            && string.Equals(backToStageFlyIn.TargetName, "BackToStageButton", System.StringComparison.Ordinal))
        {
            return;
        }

        UIStageReadyBannerFlyIn[] flyIns = GetComponents<UIStageReadyBannerFlyIn>();
        for (int i = 0; i < flyIns.Length; i++)
        {
            UIStageReadyBannerFlyIn flyIn = flyIns[i];
            if (flyIn == null)
            {
                continue;
            }

            if (string.Equals(flyIn.TargetName, "BackToStageButton", System.StringComparison.Ordinal))
            {
                backToStageFlyIn = flyIn;
                return;
            }
        }

        for (int i = 0; i < flyIns.Length; i++)
        {
            if (flyIns[i] != null && flyIns[i] != readyBannerFlyIn)
            {
                backToStageFlyIn = flyIns[i];
                return;
            }
        }
    }

    void EnsureStageScanWaveAnimatorReference()
    {
        if (stageScanWaveAnimator != null)
        {
            return;
        }

        stageScanWaveAnimator = GetComponent<StageScanWaveAnimator>();
    }

    static IEnumerator WaitForCoroutines(params Coroutine[] coroutines)
    {
        if (coroutines == null)
        {
            yield break;
        }

        for (int i = 0; i < coroutines.Length; i++)
        {
            if (coroutines[i] != null)
            {
                yield return coroutines[i];
            }
        }
    }

    void SetStageSelectionInputEnabled(bool enabled)
    {
        if (cdTurntableRotator == null)
        {
            GameObject cdObject = GameObject.Find("CD");
            if (cdObject != null)
            {
                cdTurntableRotator = cdObject.GetComponent<UITurntableDragRotator>();
            }
        }

        if (noisesVerticalScroll == null)
        {
            noisesVerticalScroll = GetComponent<UITurntableDrivenVerticalScroll>();
        }

        if (noisesSlotFocus == null)
        {
            noisesSlotFocus = GetComponent<StageNoiseSlotFocus>();
        }

        if (cdTurntableRotator != null)
        {
            cdTurntableRotator.SetInteractionEnabled(enabled);
        }

        if (noisesVerticalScroll != null)
        {
            noisesVerticalScroll.SetInteractionEnabled(enabled);
        }

        if (noisesSlotFocus != null)
        {
            noisesSlotFocus.SetInteractionEnabled(enabled);
        }
    }

    static Transform ResolveCanvasRoot()
    {
        GameObject canvasObject = GameObject.Find("Canvas");
        return canvasObject != null ? canvasObject.transform : null;
    }

    static Button FindAndSetupImageButton(Transform searchRoot, string objectName)
    {
        GameObject buttonObject = FindChildByName(searchRoot, objectName);
        if (buttonObject == null)
        {
            return null;
        }

        Image image = buttonObject.GetComponent<Image>();
        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
        {
            button = buttonObject.AddComponent<Button>();
        }

        if (image != null)
        {
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
        }
        else
        {
            button.transition = Selectable.Transition.None;
        }

        if (buttonObject.GetComponent<UIButtonPressFeedback>() == null)
        {
            buttonObject.AddComponent<UIButtonPressFeedback>();
        }

        return button;
    }

    static GameObject FindChildByName(Transform searchRoot, string objectName)
    {
        if (searchRoot == null || string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        Transform[] transforms = searchRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == objectName)
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    float EvaluateEaseOut(float normalizedTime)
    {
        normalizedTime = Mathf.Clamp01(normalizedTime);
        float power = Mathf.Max(1f, swapEaseOutPower);
        return 1f - Mathf.Pow(1f - normalizedTime, power);
    }
}
