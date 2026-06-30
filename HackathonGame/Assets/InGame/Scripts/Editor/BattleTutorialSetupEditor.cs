#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// チュートリアル用 SO・アセット・InGameTutorial シーン・Build Settings を一括セットアップ。
/// </summary>
public static class BattleTutorialSetupEditor
{
    const string MenuCreateAssets = "InGame/Setup Tutorial Assets";
    const string MenuSetupScene = "InGame/Setup Tutorial Scene (InGameTutorial)";
    const string MenuResetProgress = "InGame/Reset Tutorial Progress (Debug)";
    const string MenuAddBuild = "InGame/Add InGameTutorial To Build Settings";

    const string DataRoot = "Assets/InGame/Data";
    const string TutorialEnemyPath = DataRoot + "/Enemies/TutorialDummy.asset";
    const string TutorialBattleSettingsPath = DataRoot + "/TutorialBattleSettings.asset";
    const string TutorialStagePath = DataRoot + "/Stages/TutorialStage.asset";
    const string TutorialWavePath = DataRoot + "/Waves/Tutorial_Wave01.asset";
    const string Step1Path = DataRoot + "/Tutorials/Steps/TutorialStep_CommandTarget.asset";
    const string Step2Path = DataRoot + "/Tutorials/Steps/TutorialStep_QteAllPerfect.asset";
    const string Step3Path = DataRoot + "/Tutorials/Steps/TutorialStep_EnrageTips.asset";
    const string TutorialLoopVideoPath = "Assets/InGame/Sprites/Tutorials/Movie_005.mp4";
    const string TutorialScenePath = "Assets/Scenes/InGameTutorial.unity";
    const string TemplateEnemyPath = DataRoot + "/Enemies/WhiteEnemy.asset";
    const string TemplateBattleSettingsPath = DataRoot + "/BattleSettings.asset";

    /// <summary>Unity バッチモード用（-executeMethod BattleTutorialSetupEditor.SetupTutorialAssetsBatch）。</summary>
    public static void SetupTutorialAssetsBatch()
    {
        CreateTutorialAssets();
    }

    /// <summary>Unity バッチモード用（-executeMethod BattleTutorialSetupEditor.SetupTutorialSceneBatch）。</summary>
    public static void SetupTutorialSceneBatch()
    {
        SetupTutorialScene();
    }

    [MenuItem(MenuCreateAssets)]
    public static void CreateTutorialAssets()
    {
        EnsureDirectory(DataRoot + "/Tutorials/Steps");
        EnsureDirectory(DataRoot + "/Stages");
        EnsureDirectory(DataRoot + "/Waves");
        EnsureDirectory(DataRoot + "/Enemies");

        BattleSettingsSO battleSettings = CreateOrLoadTutorialBattleSettings();
        EnemyDataSO enemy = CreateOrLoadTutorialEnemy();
        BattleTutorialStepSO step1 = CreateOrLoadStep(
            Step1Path,
            "command_target",
            WaveTutorialMoment.AfterWaveStart,
            BattleTutorialIllustrationKind.Illustration,
            "【コマンドとターゲット選択】",
            "右下のスキルボタンを選択し、もう一度タップすると行動が確定します。まずは攻撃ボタンを2回タップして確定しましょう。攻撃時は、画面上の敵を直接タップすることでターゲットの変更が可能です。選び直したい時は「戻る」ボタンでキャンセルできます。");
        BattleTutorialStepSO step2 = CreateOrLoadStep(
            Step2Path,
            "qte_all_perfect",
            WaveTutorialMoment.BeforeFirstQte,
            BattleTutorialIllustrationKind.Video,
            "【アクションとALL Perfect】",
            "ノーツがラインに重なる瞬間に画面をタップしましょう。 すべてのノーツでPerfect判定（ALL Perfect）を取ると、攻撃が全体化し、回復は効果が大幅にアップします！ ※MissをするとPerfectの倍率が相殺されるので注意してください。");
        BattleTutorialStepSO step3 = CreateOrLoadStep(
            Step3Path,
            "enrage_tips",
            WaveTutorialMoment.OnEnrageRoundStart,
            BattleTutorialIllustrationKind.Illustration,
            "【バトルのコツ：早期決着】",
            "ターンが経過するほど、敵は狂暴化（ハウリング）して攻撃力が上がっていきます。 回復しながらの耐久戦は非常に危険です。ALL Perfectによる全体攻撃を積極的に狙い、素早くノイズを浄化しましょう。");

        WaveConfigSO wave = CreateOrLoadWave(enemy, step1, step2, step3);
        CreateOrLoadTutorialStage(wave);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BattleTutorialSetupEditor] Tutorial assets created/updated.");
    }

    [MenuItem(MenuSetupScene)]
    public static void SetupTutorialScene()
    {
        CreateTutorialAssets();

        StageConfigSO tutorialStage = AssetDatabase.LoadAssetAtPath<StageConfigSO>(TutorialStagePath);
        BattleSettingsSO tutorialBattle = AssetDatabase.LoadAssetAtPath<BattleSettingsSO>(TutorialBattleSettingsPath);
        if (tutorialStage == null || tutorialBattle == null)
        {
            Debug.LogError("[BattleTutorialSetupEditor] Tutorial assets missing. Run Setup Tutorial Assets first.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Single);
        InGameManager inGameManager = Object.FindFirstObjectByType<InGameManager>();
        if (inGameManager == null)
        {
            Debug.LogError("[BattleTutorialSetupEditor] InGameManager not found in InGameTutorial scene.");
            return;
        }

        SerializedObject igm = new SerializedObject(inGameManager);
        igm.FindProperty("_fallbackStage").objectReferenceValue = tutorialStage;
        igm.FindProperty("_battleStages").arraySize = 0;
        igm.FindProperty("_battleSettings").objectReferenceValue = tutorialBattle;
        igm.FindProperty("_tutorialRun").boolValue = true;
        igm.ApplyModifiedPropertiesWithoutUndo();

        Canvas canvas = BattleTutorialUiFactory.ResolveHostCanvas();
        BattleTutorialPopupView popup = EnsureTutorialPopup(canvas);
        WireStepIllustrationBindings(popup);
        WireAttackGuideArrow(Object.FindFirstObjectByType<CommandPanelView>());
        BattleTutorialCommandFocusOverlay commandFocus = EnsureCommandFocusOverlay(canvas);
        BattleTutorialOpeningView opening = EnsureTutorialOpening(canvas);
        BattleTutorialPresenter presenter = EnsureTutorialPresenter(inGameManager, popup, opening, commandFocus);
        igm.FindProperty("_battleTutorial").objectReferenceValue = presenter;
        igm.ApplyModifiedPropertiesWithoutUndo();

        WireQtePresenterForTutorialScene(inGameManager);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BattleTutorialSetupEditor] InGameTutorial scene wired.");
    }

    [MenuItem(MenuAddBuild)]
    public static void AddTutorialSceneToBuildSettings()
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool found = false;
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == TutorialScenePath)
            {
                scenes[i] = new EditorBuildSettingsScene(TutorialScenePath, true);
                found = true;
                break;
            }
        }

        if (!found)
        {
            scenes.Add(new EditorBuildSettingsScene(TutorialScenePath, true));
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[BattleTutorialSetupEditor] InGameTutorial added to Build Settings.");
    }

    [MenuItem(MenuResetProgress)]
    public static void ResetTutorialProgress()
    {
        BattleTutorialProgress.ResetForDebug();
        TitleIntroProgress.ResetForDebug();
        OutfitLoadoutManager.ResetFirstLaunchForDebug();
        Debug.Log("[BattleTutorialSetupEditor] Tutorial / first-play outfit progress reset.");
    }

    static void EnsureDirectory(string assetFolder)
    {
        if (!AssetDatabase.IsValidFolder(assetFolder))
        {
            string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
            string name = Path.GetFileName(assetFolder);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureDirectory(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }

    static BattleSettingsSO CreateOrLoadTutorialBattleSettings()
    {
        BattleSettingsSO existing = AssetDatabase.LoadAssetAtPath<BattleSettingsSO>(TutorialBattleSettingsPath);
        if (existing != null)
        {
            ApplyTutorialBattleSettings(existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        BattleSettingsSO template = AssetDatabase.LoadAssetAtPath<BattleSettingsSO>(TemplateBattleSettingsPath);
        BattleSettingsSO created = template != null
            ? Object.Instantiate(template)
            : ScriptableObject.CreateInstance<BattleSettingsSO>();
        created.name = "TutorialBattleSettings";
        ApplyTutorialBattleSettings(created);
        AssetDatabase.CreateAsset(created, TutorialBattleSettingsPath);
        return created;
    }

    static void ApplyTutorialBattleSettings(BattleSettingsSO settings)
    {
        SerializedObject so = new SerializedObject(settings);
        so.FindProperty("_qteMissMultiplier").floatValue = 0.5f;
        so.FindProperty("_victoryExp").intValue = 0;
        so.FindProperty("_tutorialPlayerDamageCapPerHit").intValue = 10;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static EnemyDataSO CreateOrLoadTutorialEnemy()
    {
        EnemyDataSO existing = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(TutorialEnemyPath);
        if (existing != null)
        {
            ApplyTutorialEnemyStats(existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        EnemyDataSO template = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(TemplateEnemyPath);
        EnemyDataSO created = template != null
            ? Object.Instantiate(template)
            : ScriptableObject.CreateInstance<EnemyDataSO>();
        created.name = "TutorialDummy";
        ApplyTutorialEnemyStats(created);
        AssetDatabase.CreateAsset(created, TutorialEnemyPath);
        return created;
    }

    static void ApplyTutorialEnemyStats(EnemyDataSO enemy)
    {
        SerializedObject so = new SerializedObject(enemy);
        so.FindProperty("_enemyId").stringValue = "TutorialDummy";
        so.FindProperty("_maxHp").intValue = 40;
        so.FindProperty("_speed").intValue = 2;
        so.FindProperty("_attackPower").intValue = 5;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static BattleTutorialStepSO CreateOrLoadStep(
        string path,
        string stepId,
        WaveTutorialMoment moment,
        BattleTutorialIllustrationKind illustrationKind,
        string title,
        string body)
    {
        BattleTutorialStepSO step = AssetDatabase.LoadAssetAtPath<BattleTutorialStepSO>(path);
        if (step == null)
        {
            step = ScriptableObject.CreateInstance<BattleTutorialStepSO>();
            step.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(step, path);
        }

        SerializedObject so = new SerializedObject(step);
        so.FindProperty("_stepId").stringValue = stepId;
        so.FindProperty("_moment").enumValueIndex = (int)moment;
        so.FindProperty("_illustrationKind").enumValueIndex = (int)illustrationKind;
        so.FindProperty("_title").stringValue = title;
        so.FindProperty("_body").stringValue = body;
        ApplyStepMedia(so, illustrationKind);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(step);
        return step;
    }

    static void ApplyStepMedia(SerializedObject so, BattleTutorialIllustrationKind illustrationKind)
    {
        SerializedProperty loopVideo = so.FindProperty("_loopVideo");

        switch (illustrationKind)
        {
            case BattleTutorialIllustrationKind.Video:
                loopVideo.objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<VideoClip>(TutorialLoopVideoPath);
                if (loopVideo.objectReferenceValue == null)
                {
                    Debug.LogWarning(
                        $"[BattleTutorialSetupEditor] チュートリアル動画が見つかりません: {TutorialLoopVideoPath}");
                }

                break;

            case BattleTutorialIllustrationKind.Illustration:
                loopVideo.objectReferenceValue = null;
                break;

            default:
                loopVideo.objectReferenceValue = null;
                break;
        }
    }

    static void WireAttackGuideArrow(CommandPanelView commandPanel)
    {
        if (commandPanel == null)
        {
            Debug.LogWarning("[BattleTutorialSetupEditor] CommandPanelView が見つかりません。");
            return;
        }

        Transform arrow = commandPanel.transform.Find("Arrow");
        if (arrow == null)
        {
            Debug.LogWarning("[BattleTutorialSetupEditor] CommandPanelView/Arrow が見つかりません。");
            return;
        }

        arrow.gameObject.SetActive(false);
        SerializedObject so = new SerializedObject(commandPanel);
        so.FindProperty("_attackGuideArrow").objectReferenceValue = arrow.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(commandPanel);
    }

    static void WireStepIllustrationBindings(BattleTutorialPopupView popup)
    {
        if (popup == null)
        {
            return;
        }

        Transform panel = popup.transform.Find("Panel");
        if (panel == null)
        {
            Debug.LogWarning("[BattleTutorialSetupEditor] BattleTutorialPopup/Panel が見つかりません。");
            return;
        }

        SerializedObject so = new SerializedObject(popup);
        SerializedProperty bindings = so.FindProperty("_stepIllustrationBindings");
        bindings.arraySize = 2;
        SetStepIllustrationBinding(bindings, 0, "command_target", panel, "Tutorial_001");
        SetStepIllustrationBinding(bindings, 1, "enrage_tips", panel, "Tutorial_003");
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(popup);
    }

    static void SetStepIllustrationBinding(
        SerializedProperty bindings,
        int index,
        string stepId,
        Transform panel,
        string imageObjectName)
    {
        SerializedProperty element = bindings.GetArrayElementAtIndex(index);
        element.FindPropertyRelative("_stepId").stringValue = stepId;
        Transform imageTransform = panel.Find(imageObjectName);
        Image image = imageTransform != null ? imageTransform.GetComponent<Image>() : null;
        element.FindPropertyRelative("_image").objectReferenceValue = image;
        if (image == null)
        {
            Debug.LogWarning(
                $"[BattleTutorialSetupEditor] Panel 配下に {imageObjectName} の Image が見つかりません。");
        }
    }

    static WaveConfigSO CreateOrLoadWave(
        EnemyDataSO enemy,
        BattleTutorialStepSO step1,
        BattleTutorialStepSO step2,
        BattleTutorialStepSO step3)
    {
        WaveConfigSO wave = AssetDatabase.LoadAssetAtPath<WaveConfigSO>(TutorialWavePath);
        if (wave == null)
        {
            wave = ScriptableObject.CreateInstance<WaveConfigSO>();
            wave.name = "Tutorial_Wave01";
            AssetDatabase.CreateAsset(wave, TutorialWavePath);
        }

        SerializedObject so = new SerializedObject(wave);
        so.FindProperty("_waveIndex").intValue = 0;
        SerializedProperty enemies = so.FindProperty("_enemies");
        enemies.arraySize = 1;
        enemies.GetArrayElementAtIndex(0).objectReferenceValue = enemy;
        SerializedProperty steps = so.FindProperty("_tutorialSteps");
        steps.arraySize = 3;
        steps.GetArrayElementAtIndex(0).objectReferenceValue = step1;
        steps.GetArrayElementAtIndex(1).objectReferenceValue = step2;
        steps.GetArrayElementAtIndex(2).objectReferenceValue = step3;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(wave);
        return wave;
    }

    static StageConfigSO CreateOrLoadTutorialStage(WaveConfigSO wave)
    {
        StageConfigSO stage = AssetDatabase.LoadAssetAtPath<StageConfigSO>(TutorialStagePath);
        if (stage == null)
        {
            stage = ScriptableObject.CreateInstance<StageConfigSO>();
            stage.name = "TutorialStage";
            AssetDatabase.CreateAsset(stage, TutorialStagePath);
        }

        SerializedObject so = new SerializedObject(stage);
        so.FindProperty("_stageId").stringValue = TutorialStageIds.TutorialStage;
        so.FindProperty("_displayName").stringValue = "チュートリアル";
        so.FindProperty("_isTutorialStage").boolValue = true;
        SerializedProperty waves = so.FindProperty("_waves");
        waves.arraySize = 1;
        waves.GetArrayElementAtIndex(0).objectReferenceValue = wave;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(stage);
        return stage;
    }

    static BattleTutorialPopupView EnsureTutorialPopup(Canvas canvas)
    {
        return BattleTutorialUiFactory.CreatePopupUnderCanvas(canvas);
    }

    static BattleTutorialCommandFocusOverlay EnsureCommandFocusOverlay(Canvas canvas)
    {
        return BattleTutorialUiFactory.CreateCommandFocusOverlay(canvas);
    }

    static BattleTutorialOpeningView EnsureTutorialOpening(Canvas canvas)
    {
        return BattleTutorialUiFactory.CreateOpeningOverlay(canvas);
    }

    static void WireQtePresenterForTutorialScene(InGameManager inGameManager)
    {
        QtePresenter qtePresenter = Object.FindFirstObjectByType<QtePresenter>(FindObjectsInactive.Include);
        if (qtePresenter == null)
        {
            Debug.LogWarning("[BattleTutorialSetupEditor] QtePresenter が見つかりません。");
            return;
        }

        SerializedObject igm = new SerializedObject(inGameManager);
        BattleCameraController battleCamera =
            igm.FindProperty("_battleCamera").objectReferenceValue as BattleCameraController;
        TargetSelectView targetSelectView =
            igm.FindProperty("_targetSelectView").objectReferenceValue as TargetSelectView;

        SerializedObject qte = new SerializedObject(qtePresenter);
        qte.FindProperty("_battleCamera").objectReferenceValue = battleCamera;
        qte.FindProperty("_targetSelectView").objectReferenceValue = targetSelectView;
        qte.ApplyModifiedPropertiesWithoutUndo();

        if (battleCamera == null || targetSelectView == null)
        {
            Debug.LogWarning(
                "[BattleTutorialSetupEditor] QtePresenter に BattleCamera / TargetSelectView を割り当てられませんでした。"
                + " InGameManager の参照を確認してください。",
                qtePresenter);
        }
    }

    static BattleTutorialPresenter EnsureTutorialPresenter(
        InGameManager inGameManager,
        BattleTutorialPopupView popup,
        BattleTutorialOpeningView opening,
        BattleTutorialCommandFocusOverlay commandFocus)
    {
        Transform managers = inGameManager.transform.parent;
        if (managers == null)
        {
            managers = inGameManager.transform;
        }

        BattleTutorialPresenter presenter = managers.GetComponentInChildren<BattleTutorialPresenter>(true);
        if (presenter == null)
        {
            var go = new GameObject("BattleTutorialPresenter");
            go.transform.SetParent(managers, false);
            presenter = go.AddComponent<BattleTutorialPresenter>();
        }

        SerializedObject so = new SerializedObject(presenter);
        so.FindProperty("_popupView").objectReferenceValue = popup;
        so.FindProperty("_openingView").objectReferenceValue = opening;
        so.FindProperty("_commandFocusOverlay").objectReferenceValue = commandFocus;
        so.FindProperty("_inGameManager").objectReferenceValue = inGameManager;
        so.ApplyModifiedPropertiesWithoutUndo();
        return presenter;
    }
}

#endif
