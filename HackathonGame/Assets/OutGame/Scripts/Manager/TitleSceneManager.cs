using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TitleSceneManager : MonoBehaviour
{
    public const string DefaultOpeningBgmPath = "Assets/OutGame/Audio/opening_bgm.wav";
    const int IntroStepCount = 5;

    [Header("UI")]
    [Tooltip("TMP テキスト用（任意）。")]
    [SerializeField] private TMP_Text blinkText;
    [Tooltip("初回 Panel 表示中のタップ促し（Canvas > BlinkingTap）。")]
    [SerializeField] private Graphic firstPlayBlinkingTapGraphic;
    [Tooltip("2 回目以降のタイトル画面（Canvas > GameTitle）。")]
    [SerializeField] private GameObject gameTitle;
    [Tooltip("2 回目以降のタップ促し（Canvas > BlinkingTapToStart）。")]
    [SerializeField] private Graphic blinkingTapToStartGraphic;

    [Header("First Play Intro")]
    [Tooltip("Canvas > Panel。初回のみ 1→5 の画像を順に表示。")]
    [SerializeField] private GameObject firstPlayPanel;
    [SerializeField] private GameObject[] introStepObjects;
    [SerializeField] private float introStepRevealDuration = 0.55f;
    [Tooltip("出現時に下から浮き上がる距離（px）。")]
    [SerializeField] private float introStepFloatUpOffset = 36f;

    [Header("Opening BGM")]
    [Tooltip("Assign Assets/OutGame/Audio/opening_bgm.wav (loops while on Title).")]
    [SerializeField] private AudioClip openingBgm;
    [SerializeField] private bool playOpeningBgmOnStart = true;
    [SerializeField] [Range(0f, 1f)] private float openingBgmVolume = 1f;

    [Header("Blink / Breath")]
    [SerializeField] private float breathCycleDuration = 2.4f;
    [SerializeField] [Range(0f, 1f)] private float breathMinAlpha = 0.2f;
    [SerializeField] [Range(0f, 1f)] private float breathMaxAlpha = 1f;

    AudioSource _openingBgmSource;
    Coroutine _blinkCoroutine;
    Coroutine _introStepRevealCoroutine;
    Vector2[] _introStepBasePositions;
    bool _firstPlayIntroActive;
    int _currentIntroStep = 1;

    void Awake()
    {
        DisableLegacyTapStartButtonObject();
        EnsureFirstPlayIntroReferences();
        SetupFirstPlayIntroState();

        EnsureOpeningBgmReference();
        if (playOpeningBgmOnStart)
        {
            PlayOpeningBgm();
        }
    }

    void Start()
    {
        StartBreathingAnimation();
    }

    void Update()
    {
        if (!TryGetScreenTapBegan(out _))
        {
            return;
        }

        HandleScreenTap();
    }

    void OnDestroy()
    {
        if (_blinkCoroutine != null)
        {
            StopCoroutine(_blinkCoroutine);
            _blinkCoroutine = null;
        }

        if (_introStepRevealCoroutine != null)
        {
            StopCoroutine(_introStepRevealCoroutine);
            _introStepRevealCoroutine = null;
        }

        StopOpeningBgm();
    }

    void HandleScreenTap()
    {
        if (_firstPlayIntroActive)
        {
            if (_currentIntroStep < IntroStepCount)
            {
                ShowIntroStep(_currentIntroStep + 1);
                return;
            }

            CompleteFirstPlayIntro();
            LoadNextScene();
            return;
        }

        LoadNextScene();
    }

    static bool TryGetScreenTapBegan(out Vector2 screenPosition)
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            screenPosition = touch.position;
            return touch.phase == TouchPhase.Began;
        }

        screenPosition = Input.mousePosition;
        return Input.GetMouseButtonDown(0);
    }

    static void DisableLegacyTapStartButtonObject()
    {
        var tapObject = GameObject.Find("TapStartButton");
        if (tapObject != null)
        {
            tapObject.SetActive(false);
        }
    }

    void LoadNextScene()
    {
        if (SceneTransferManager.Instance == null)
        {
            Debug.LogError("[TitleSceneManager] SceneTransferManager が見つかりません。");
            return;
        }

        StopOpeningBgm();
        string nextScene = BattleTutorialProgress.IsCompleted
            ? SceneNames.Home
            : SceneNames.InGameTutorial;
        SceneTransferManager.Instance.LoadNewScene(nextScene);
    }

    void EnsureFirstPlayIntroReferences()
    {
        if (firstPlayPanel == null)
        {
            var panelObject = GameObject.Find("Panel");
            if (panelObject != null)
            {
                firstPlayPanel = panelObject;
            }
        }

        if (introStepObjects == null || introStepObjects.Length != IntroStepCount)
        {
            CacheIntroStepsFromPanel();
        }
        else
        {
            EnsureIntroStepBasePositions();
        }

        if (firstPlayBlinkingTapGraphic == null)
        {
            var blinkObject = GameObject.Find("BlinkingTap");
            if (blinkObject == null)
            {
                blinkObject = GameObject.Find("Blinkingtap");
            }

            if (blinkObject != null)
            {
                firstPlayBlinkingTapGraphic = blinkObject.GetComponent<Graphic>();
            }
        }

        if (gameTitle == null)
        {
            gameTitle = GameObject.Find("GameTitle");
        }

        if (blinkingTapToStartGraphic == null)
        {
            var tapToStartObject = GameObject.Find("BlinkingTapToStart");
            if (tapToStartObject != null)
            {
                blinkingTapToStartGraphic = tapToStartObject.GetComponent<Graphic>();
            }
        }
    }

    void CacheIntroStepsFromPanel()
    {
        if (firstPlayPanel == null)
        {
            return;
        }

        introStepObjects = new GameObject[IntroStepCount];
        for (int i = 0; i < IntroStepCount; i++)
        {
            Transform step = firstPlayPanel.transform.Find((i + 1).ToString());
            introStepObjects[i] = step != null ? step.gameObject : null;
        }

        EnsureIntroStepBasePositions();
    }

    void EnsureIntroStepBasePositions()
    {
        if (introStepObjects == null || introStepObjects.Length != IntroStepCount)
        {
            return;
        }

        _introStepBasePositions = new Vector2[IntroStepCount];
        for (int i = 0; i < IntroStepCount; i++)
        {
            GameObject stepObject = introStepObjects[i];
            RectTransform rect = stepObject != null ? stepObject.GetComponent<RectTransform>() : null;
            _introStepBasePositions[i] = rect != null ? rect.anchoredPosition : Vector2.zero;
        }
    }

    void SetupFirstPlayIntroState()
    {
        _firstPlayIntroActive = !BattleTutorialProgress.IsCompleted
            && firstPlayPanel != null
            && HasValidIntroSteps();

        if (!_firstPlayIntroActive)
        {
            if (firstPlayPanel != null)
            {
                firstPlayPanel.SetActive(false);
            }

            ApplyReturningTitleUi();
            return;
        }

        ApplyFirstPlayIntroUi();
        firstPlayPanel.SetActive(true);
        ShowIntroStep(1);
        StartBreathingAnimation();
    }

    void ApplyFirstPlayIntroUi()
    {
        SetGameObjectActive(gameTitle, false);
        SetGraphicObjectActive(blinkingTapToStartGraphic, false);
        SetGraphicObjectActive(firstPlayBlinkingTapGraphic, true);
    }

    void ApplyReturningTitleUi()
    {
        SetGameObjectActive(gameTitle, true);
        SetGraphicObjectActive(firstPlayBlinkingTapGraphic, false);
        SetGraphicObjectActive(blinkingTapToStartGraphic, true);
        StartBreathingAnimation();
    }

    static void SetGameObjectActive(GameObject target, bool visible)
    {
        if (target != null)
        {
            target.SetActive(visible);
        }
    }

    static void SetGraphicObjectActive(Graphic graphic, bool visible)
    {
        if (graphic != null)
        {
            graphic.gameObject.SetActive(visible);
        }
    }

    bool HasValidIntroSteps()
    {
        if (introStepObjects == null || introStepObjects.Length != IntroStepCount)
        {
            return false;
        }

        for (int i = 0; i < IntroStepCount; i++)
        {
            if (introStepObjects[i] == null)
            {
                Debug.LogWarning(
                    $"[TitleSceneManager] Panel の子に {i + 1} が見つかりません。初回イントロをスキップします。",
                    this);
                return false;
            }
        }

        return true;
    }

    void ShowIntroStep(int step)
    {
        _currentIntroStep = Mathf.Clamp(step, 1, IntroStepCount);

        if (_introStepRevealCoroutine != null)
        {
            StopCoroutine(_introStepRevealCoroutine);
            _introStepRevealCoroutine = null;
        }

        for (int i = 0; i < IntroStepCount; i++)
        {
            introStepObjects[i].SetActive(false);
        }

        GameObject activeStep = introStepObjects[_currentIntroStep - 1];
        _introStepRevealCoroutine = StartCoroutine(RevealIntroStepRoutine(activeStep, _currentIntroStep - 1));
    }

    IEnumerator RevealIntroStepRoutine(GameObject stepObject, int stepIndex)
    {
        if (stepObject == null)
        {
            yield break;
        }

        stepObject.SetActive(true);

        Graphic graphic = stepObject.GetComponent<Graphic>();
        if (graphic == null)
        {
            graphic = stepObject.GetComponentInChildren<Graphic>(true);
        }

        RectTransform rect = stepObject.GetComponent<RectTransform>();
        if (graphic == null || rect == null)
        {
            yield break;
        }

        Vector2 basePosition = GetIntroStepBasePosition(stepIndex, rect);
        Color targetColor = graphic.color;
        targetColor.a = 1f;
        float startOffset = Mathf.Max(0f, introStepFloatUpOffset);
        Vector2 startPosition = basePosition + new Vector2(0f, -startOffset);

        float duration = Mathf.Max(0.05f, introStepRevealDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            Color c = targetColor;
            c.a = eased;
            graphic.color = c;
            rect.anchoredPosition = Vector2.Lerp(startPosition, basePosition, eased);
            yield return null;
        }

        graphic.color = targetColor;
        rect.anchoredPosition = basePosition;
        _introStepRevealCoroutine = null;
    }

    Vector2 GetIntroStepBasePosition(int stepIndex, RectTransform rect)
    {
        if (_introStepBasePositions != null
            && stepIndex >= 0
            && stepIndex < _introStepBasePositions.Length)
        {
            return _introStepBasePositions[stepIndex];
        }

        return rect.anchoredPosition;
    }

    void CompleteFirstPlayIntro()
    {
        _firstPlayIntroActive = false;

        if (firstPlayPanel != null)
        {
            firstPlayPanel.SetActive(false);
        }
    }

    void StartBreathingAnimation()
    {
        if (_blinkCoroutine != null)
        {
            StopCoroutine(_blinkCoroutine);
            _blinkCoroutine = null;
        }

        if (TryGetActiveBreathingGraphic(out Graphic graphic))
        {
            ResetGraphicAlphaForBreathing(graphic);
            _blinkCoroutine = StartCoroutine(BreathingGraphicRoutine(graphic));
            return;
        }

        if (blinkText != null && blinkText.gameObject.activeInHierarchy)
        {
            _blinkCoroutine = StartCoroutine(BlinkTextRoutine());
        }
    }

    bool TryGetActiveBreathingGraphic(out Graphic graphic)
    {
        if (_firstPlayIntroActive)
        {
            graphic = firstPlayBlinkingTapGraphic;
            return graphic != null && graphic.gameObject.activeInHierarchy;
        }

        graphic = blinkingTapToStartGraphic;
        return graphic != null && graphic.gameObject.activeInHierarchy;
    }

    static void ResetGraphicAlphaForBreathing(Graphic graphic)
    {
        Color color = graphic.color;
        color.a = 1f;
        graphic.color = color;
    }

    void EnsureOpeningBgmReference()
    {
        if (openingBgm != null)
        {
            return;
        }

#if UNITY_EDITOR
        openingBgm = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultOpeningBgmPath);
#endif
    }

    void PlayOpeningBgm()
    {
        if (openingBgm == null)
        {
            Debug.LogWarning(
                $"[TitleSceneManager] Opening BGM が未設定です。Inspector で {DefaultOpeningBgmPath} を割り当ててください。",
                this);
            return;
        }

        EnsureOpeningBgmSource();
        _openingBgmSource.clip = openingBgm;
        _openingBgmSource.loop = true;
        _openingBgmSource.volume = openingBgmVolume;
        _openingBgmSource.Play();
    }

    void EnsureOpeningBgmSource()
    {
        if (_openingBgmSource != null)
        {
            return;
        }

        _openingBgmSource = GetComponent<AudioSource>();
        if (_openingBgmSource == null)
        {
            _openingBgmSource = gameObject.AddComponent<AudioSource>();
        }

        _openingBgmSource.playOnAwake = false;
        _openingBgmSource.loop = true;
        _openingBgmSource.spatialBlend = 0f;
    }

    void StopOpeningBgm()
    {
        if (_openingBgmSource != null && _openingBgmSource.isPlaying)
        {
            _openingBgmSource.Stop();
        }
    }

    IEnumerator BreathingGraphicRoutine(Graphic graphic)
    {
        Color baseColor = graphic.color;
        float minAlpha = Mathf.Min(breathMinAlpha, breathMaxAlpha);
        float maxAlpha = Mathf.Max(breathMinAlpha, breathMaxAlpha);
        float cycle = Mathf.Max(0.1f, breathCycleDuration);

        while (true)
        {
            float phase = (Mathf.Sin((Time.time / cycle) * Mathf.PI * 2f) + 1f) * 0.5f;
            baseColor.a = Mathf.Lerp(minAlpha, maxAlpha, phase);
            graphic.color = baseColor;
            yield return null;
        }
    }

    IEnumerator BlinkTextRoutine()
    {
        Color originalColor = blinkText.color;
        float halfCycle = Mathf.Max(0.1f, breathCycleDuration * 0.5f);

        while (true)
        {
            float timer = 0f;
            while (timer < halfCycle)
            {
                timer += Time.deltaTime;
                float phase = timer / halfCycle;
                originalColor.a = Mathf.Lerp(breathMaxAlpha, breathMinAlpha, phase);
                blinkText.color = originalColor;
                yield return null;
            }

            timer = 0f;
            while (timer < halfCycle)
            {
                timer += Time.deltaTime;
                float phase = timer / halfCycle;
                originalColor.a = Mathf.Lerp(breathMinAlpha, breathMaxAlpha, phase);
                blinkText.color = originalColor;
                yield return null;
            }
        }
    }
}
