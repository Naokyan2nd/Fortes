#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HomeSceneManager))]
public sealed class HomeSceneManagerEditor : Editor
{
    SerializedProperty _gaugeMetersPerCurrent;
    SerializedProperty _gaugeMax;
    SerializedProperty _gaugeShowMaxInText;
    SerializedProperty _noisesAmountText;
    SerializedProperty _totalDistanceDisplayText;
    SerializedProperty _gaugeFillImage;
    SerializedProperty _gaugeValueText;
    SerializedProperty _useDailyDistanceSchedule;
    SerializedProperty _schedulePlayStartHour;
    SerializedProperty _schedulePlayStartMinute;
    SerializedProperty _scheduleUseDebugTime;
    SerializedProperty _scheduleDebugYear;
    SerializedProperty _scheduleDebugMonth;
    SerializedProperty _scheduleDebugDay;
    SerializedProperty _scheduleDebugHour;
    SerializedProperty _scheduleDebugMinute;
    SerializedProperty _firstPlayGivenDistanceMeters;
    SerializedProperty _scheduleDebugRolloverAppliedToday;
    SerializedProperty _scheduleDebugPlayMeters;
    SerializedProperty _scheduleDebugStoredPreviousDayMeters;
    SerializedProperty _scheduleDebugBackgroundAccumulatedMeters;
    SerializedProperty _scheduleDebugRecordedRealPreviousDayMeters;

    SerializedProperty _enableLevelGaugeSecretDistanceDebugTap;
    SerializedProperty _levelGaugeSecretTapCountRequired;
    SerializedProperty _levelGaugeSecretTapWindowSeconds;

    SerializedProperty _playerLevelConfig;
    SerializedProperty _useManualLevelProgressForDebug;
    SerializedProperty _levelDisplayText;
    SerializedProperty _expDisplayText;
    SerializedProperty _manualLevel;
    SerializedProperty _manualCurrentExp;

    SerializedProperty _useManualCombatStatsForDebug;
    SerializedProperty _manualCombatAttack;
    SerializedProperty _manualCombatMaxHp;

    SerializedProperty _stageVictoryRewards;
    SerializedProperty _useManualBattleRewardsForDebug;
    SerializedProperty _manualSuperRareRewardCount;
    SerializedProperty _manualRareRewardCount;
    SerializedProperty _manualNormalRewardCount;

    bool _distanceInspectorRepaintHooked;

    void OnEnable()
    {
        _gaugeMetersPerCurrent = serializedObject.FindProperty("gaugeMetersPerCurrent");
        _gaugeMax = serializedObject.FindProperty("gaugeMax");
        _gaugeShowMaxInText = serializedObject.FindProperty("gaugeShowMaxInText");
        _noisesAmountText = serializedObject.FindProperty("noisesAmountText");
        _totalDistanceDisplayText = serializedObject.FindProperty("totalDistanceDisplayText");
        _gaugeFillImage = serializedObject.FindProperty("gaugeFillImage");
        _gaugeValueText = serializedObject.FindProperty("gaugeValueText");
        _useDailyDistanceSchedule = serializedObject.FindProperty("useDailyDistanceSchedule");
        _schedulePlayStartHour = serializedObject.FindProperty("schedulePlayStartHour");
        _schedulePlayStartMinute = serializedObject.FindProperty("schedulePlayStartMinute");
        _scheduleUseDebugTime = serializedObject.FindProperty("scheduleUseDebugTime");
        _scheduleDebugYear = serializedObject.FindProperty("scheduleDebugYear");
        _scheduleDebugMonth = serializedObject.FindProperty("scheduleDebugMonth");
        _scheduleDebugDay = serializedObject.FindProperty("scheduleDebugDay");
        _scheduleDebugHour = serializedObject.FindProperty("scheduleDebugHour");
        _scheduleDebugMinute = serializedObject.FindProperty("scheduleDebugMinute");
        _firstPlayGivenDistanceMeters = serializedObject.FindProperty("firstPlayGivenDistanceMeters");
        _scheduleDebugRolloverAppliedToday = serializedObject.FindProperty("scheduleDebugRolloverAppliedToday");
        _scheduleDebugPlayMeters = serializedObject.FindProperty("scheduleDebugPlayMeters");
        _scheduleDebugStoredPreviousDayMeters = serializedObject.FindProperty("scheduleDebugStoredPreviousDayMeters");
        _scheduleDebugBackgroundAccumulatedMeters =
            serializedObject.FindProperty("scheduleDebugBackgroundAccumulatedMeters");
        _scheduleDebugRecordedRealPreviousDayMeters =
            serializedObject.FindProperty("scheduleDebugRecordedRealPreviousDayMeters");

        _enableLevelGaugeSecretDistanceDebugTap = serializedObject.FindProperty("enableLevelGaugeSecretDistanceDebugTap");
        _levelGaugeSecretTapCountRequired = serializedObject.FindProperty("levelGaugeSecretTapCountRequired");
        _levelGaugeSecretTapWindowSeconds = serializedObject.FindProperty("levelGaugeSecretTapWindowSeconds");

        _playerLevelConfig = serializedObject.FindProperty("playerLevelConfig");
        _useManualLevelProgressForDebug = serializedObject.FindProperty("useManualLevelProgressForDebug");
        _levelDisplayText = serializedObject.FindProperty("levelDisplayText");
        _expDisplayText = serializedObject.FindProperty("expDisplayText");
        _manualLevel = serializedObject.FindProperty("manualLevel");
        _manualCurrentExp = serializedObject.FindProperty("manualCurrentExp");

        _useManualCombatStatsForDebug = serializedObject.FindProperty("useManualCombatStatsForDebug");
        _manualCombatAttack = serializedObject.FindProperty("manualCombatAttack");
        _manualCombatMaxHp = serializedObject.FindProperty("manualCombatMaxHp");

        _stageVictoryRewards = serializedObject.FindProperty("stageVictoryRewards");
        _useManualBattleRewardsForDebug = serializedObject.FindProperty("useManualBattleRewardsForDebug");
        _manualSuperRareRewardCount = serializedObject.FindProperty("manualSuperRareRewardCount");
        _manualRareRewardCount = serializedObject.FindProperty("manualRareRewardCount");
        _manualNormalRewardCount = serializedObject.FindProperty("manualNormalRewardCount");

        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        _distanceInspectorRepaintHooked = true;
    }

    void OnDisable()
    {
        if (_distanceInspectorRepaintHooked)
        {
            EditorApplication.update -= OnEditorUpdate;
            _distanceInspectorRepaintHooked = false;
        }
    }

    void OnEditorUpdate()
    {
        if (!Application.isPlaying || target == null)
        {
            return;
        }

        serializedObject.Update();
        Repaint();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "gaugeMetersPerCurrent",
            "gaugeMax",
            "gaugeShowMaxInText",
            "noisesAmountText",
            "totalDistanceDisplayText",
            "gaugeFillImage",
            "gaugeValueText",
            "scheduleDebugRolloverAppliedToday",
            "scheduleDebugPlayMeters",
            "scheduleDebugStoredPreviousDayMeters",
            "scheduleDebugBackgroundAccumulatedMeters",
            "scheduleDebugRecordedRealPreviousDayMeters",
            "enableLevelGaugeSecretDistanceDebugTap",
            "levelGaugeSecretTapCountRequired",
            "levelGaugeSecretTapWindowSeconds",
            "playerLevelConfig",
            "useManualLevelProgressForDebug",
            "levelDisplayText",
            "expDisplayText",
            "manualLevel",
            "manualCurrentExp",
            "useManualCombatStatsForDebug",
            "manualCombatAttack",
            "manualCombatMaxHp",
            "stageVictoryRewards",
            "useManualBattleRewardsForDebug",
            "manualSuperRareRewardCount",
            "manualRareRewardCount",
            "manualNormalRewardCount");

        DrawDailyDistanceScheduleSection();

        EditorGUILayout.Space(6f);
        DrawNoisesAmountSection();

        EditorGUILayout.Space(6f);
        DrawLevelGaugeSecretDebugTapSection();

        EditorGUILayout.Space(6f);
        DrawPlayerLevelSection();

        EditorGUILayout.Space(6f);
        DrawCombatStatsSection();

        EditorGUILayout.Space(6f);
        DrawBattleRewardsSection();

        serializedObject.ApplyModifiedProperties();
    }

    void DrawNoisesAmountSection()
    {
        var home = (HomeSceneManager)target;

        EditorGUILayout.LabelField("NoisesAmount (Distance + gauge + text)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "DistanceTravelled 来自 GPS / 存档累计。\n" +
            "开启 Daily Schedule 时：首次进入游戏赠送 First Play Given Distance；次日 Rollover 起同步前一天真实行走距离。",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.LabelField("Rules", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(
            _gaugeMetersPerCurrent,
            new GUIContent(
                "Meters Per Noise",
                "Walk distance per +1 on NoisesAmount. MainScene victory subtracts this many meters."));
        EditorGUILayout.PropertyField(
            _gaugeMax,
            new GUIContent("Max Noises", "Denominator on NoisesAmount (e.g. 4/12 → 12)."));
        EditorGUILayout.PropertyField(_gaugeShowMaxInText, new GUIContent("Show Max In Text"));
        bool distanceRulesChanged = EditorGUI.EndChangeCheck();

        if (distanceRulesChanged)
        {
            serializedObject.ApplyModifiedProperties();
            home.ApplyInspectorDistanceToGame();
        }

        DrawNoisesAmountLivePreview();

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("UI References", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(_noisesAmountText, new GUIContent("Noises Amount Text"));
        EditorGUILayout.PropertyField(_totalDistanceDisplayText, new GUIContent("Distance Travelled Text"));
        EditorGUILayout.PropertyField(_gaugeFillImage, new GUIContent("Gauge Fill Image"));
        EditorGUILayout.PropertyField(
            _gaugeValueText,
            new GUIContent(
                "Gauge Value Text (fallback)",
                "Used when Noises Amount Text is unset. Often the same TMP as NoisesAmount."));
    }

    void DrawDailyDistanceScheduleSection()
    {
        var home = (HomeSceneManager)target;

        EditorGUILayout.HelpBox(
            "Daily Distance Schedule（脚本字段在上方默认区域）：\n" +
            "• 第 1 天：DistanceTravelled = First Play Given Distance；GPS 走路只记入 Background\n" +
            "• 第 2 天 Rollover：DistanceTravelled 清零 → 填入昨天 Background → Background 清零重测\n" +
            "• To Scan 全天可用",
            MessageType.Info);

        if (!Application.isPlaying || _useDailyDistanceSchedule == null || !_useDailyDistanceSchedule.boolValue)
        {
            return;
        }

        EditorGUI.BeginChangeCheck();
        home.SyncDailyDistanceScheduleConfigForEditor();

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Runtime (read-only)", EditorStyles.miniBoldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            DrawOptionalProperty(_scheduleDebugRolloverAppliedToday, "Rollover Applied Today");
            DrawOptionalProperty(_scheduleDebugPlayMeters, "DistanceTravelled (m)");
            DrawOptionalProperty(_scheduleDebugBackgroundAccumulatedMeters, "Background GPS (m)");
            DrawOptionalProperty(_scheduleDebugStoredPreviousDayMeters, "Stored Previous Day (m)");
            DrawOptionalProperty(_scheduleDebugRecordedRealPreviousDayMeters, "Recorded Real Previous Day (m)");
        }

        if (EditorGUI.EndChangeCheck() && Application.isPlaying)
        {
            serializedObject.ApplyModifiedProperties();
            home.ApplyInspectorDistanceToGame();
        }
    }

    static void DrawOptionalProperty(SerializedProperty property, string label)
    {
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label));
        }
    }

    void DrawLevelGaugeSecretDebugTapSection()
    {
        var home = (HomeSceneManager)target;

        EditorGUILayout.LabelField("Level Gauge Secret Debug Tap", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "开启后：在 Play 模式下快速点击 LevelGauge（默认 2 秒内连点 10 次）会进入 DistanceDebugScene。",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(
            _enableLevelGaugeSecretDistanceDebugTap,
            new GUIContent("Enable Secret Tap"));
        using (new EditorGUI.DisabledScope(!_enableLevelGaugeSecretDistanceDebugTap.boolValue))
        {
            EditorGUILayout.PropertyField(
                _levelGaugeSecretTapCountRequired,
                new GUIContent("Required Tap Count"));
            EditorGUILayout.PropertyField(
                _levelGaugeSecretTapWindowSeconds,
                new GUIContent("Tap Window (seconds)"));
        }

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            if (Application.isPlaying)
            {
                home.RefreshLevelGaugeSecretDebugTapBinding();
            }
        }
    }

    void DrawNoisesAmountLivePreview()
    {
        var home = (HomeSceneManager)target;
        float metersPerNoise = Mathf.Max(1f, _gaugeMetersPerCurrent.floatValue);
        int maxNoises = Mathf.Max(0, Mathf.FloorToInt(_gaugeMax.floatValue));

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Computed (read-only)", EditorStyles.miniBoldLabel);

        if (!Application.isPlaying)
        {
            float firstPlayMeters = Mathf.Max(0f, _firstPlayGivenDistanceMeters.floatValue);
            int firstPlayNoises = OutGameScanNoiseRevealCount.ComputeNoiseCountFromDistance(
                firstPlayMeters,
                metersPerNoise,
                maxNoises);
            EditorGUILayout.HelpBox(
                $"编辑模式预览：进入 Play 后显示实时距离。若开启 Daily Schedule，首次游玩约为 {firstPlayMeters:F0} m（约 {firstPlayNoises} noises）。",
                MessageType.Info);
            return;
        }

        if (_useDailyDistanceSchedule.boolValue)
        {
            home.SyncDailyDistanceScheduleConfigForEditor();
        }

        float distance = OutGameScanNoiseRevealCount.GetResolvedTotalDistanceMeters();
        int noiseCount = OutGameScanNoiseRevealCount.ComputeNoiseCountFromDistance(
            distance,
            metersPerNoise,
            maxNoises);
        float fill = OutGameScanNoiseRevealCount.GetGaugeFillRatio(distance, metersPerNoise, maxNoises);

        EditorGUILayout.IntField("NoisesAmount (derived)", noiseCount);
        EditorGUILayout.FloatField("Gauge Fill (derived)", fill);
    }

    void DrawPlayerLevelSection()
    {
        EditorGUILayout.LabelField("Player Level & Experience", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(_playerLevelConfig, new GUIContent("Player Level Config"));
        DrawMaxExpPerLevelPreview();

        EditorGUILayout.PropertyField(
            _useManualLevelProgressForDebug,
            new GUIContent(
                "Use Manual Level Progress",
                "On: edit Manual Level/Exp below for UI preview.\nOff: read saved progress from PlayerLevelManager (battle victory EXP, etc.)."));

        EditorGUILayout.PropertyField(_levelDisplayText, new GUIContent("Level Display Text"));
        EditorGUILayout.PropertyField(_expDisplayText, new GUIContent("Experience Display Text"));

        bool useManual = _useManualLevelProgressForDebug.boolValue;
        using (new EditorGUI.DisabledScope(!useManual))
        {
            EditorGUILayout.PropertyField(_manualLevel, new GUIContent("Manual Level"));
            EditorGUILayout.PropertyField(_manualCurrentExp, new GUIContent("Manual Current Exp"));
            EditorGUILayout.HelpBox(
                "Manual values are saved to PlayerLevelManager so ResultScene and other scenes show the same exp. Max exp comes from Player Level Config.",
                MessageType.None);
        }

        if (!useManual)
        {
            EditorGUILayout.Space(4f);
            DrawSavedProgressPreview();
        }
    }

    void DrawCombatStatsSection()
    {
        var home = (HomeSceneManager)target;

        EditorGUILayout.LabelField("Combat Stats (Attack / Max HP)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Base attack & HP per level come from Player Level Config (below). Equipment adds weapon attack and top/bottom defense as bonus HP. MainScene uses the resolved totals.",
            MessageType.None);

        DrawAttackHpPerLevelEditor();

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Debug Override", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(
            _useManualCombatStatsForDebug,
            new GUIContent(
                "Use Manual Combat Stats",
                "On: MainScene attack damage and player Max HP use Manual Attack / Max HP (ignores level + equipment)."));
        using (new EditorGUI.DisabledScope(!_useManualCombatStatsForDebug.boolValue))
        {
            EditorGUILayout.PropertyField(_manualCombatAttack, new GUIContent("Manual Attack"));
            EditorGUILayout.PropertyField(_manualCombatMaxHp, new GUIContent("Manual Max HP"));
        }

        DrawCombatStatsLivePreview(home);

        if (GUILayout.Button("Apply Combat Stats Override"))
        {
            home.RefreshCombatStatsBinding();
        }
    }

    void DrawBattleRewardsSection()
    {
        var home = (HomeSceneManager)target;

        EditorGUILayout.LabelField("Battle Rewards (SuperRare / Rare / Normal)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Per-stage clear grants (editable below). Victory in MainScene adds these to saved inventory.\n"
            + "Default: SuperRare stage → 5 SR + 5 R; Rare → 5 R + 5 N; Normal → 5 N.",
            MessageType.None);

        EditorGUILayout.LabelField("Victory Grants Per Stage", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(
            _stageVictoryRewards,
            new GUIContent("Stage Victory Rewards"),
            true);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Current Inventory", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(
            _useManualBattleRewardsForDebug,
            new GUIContent(
                "Use Manual Battle Rewards",
                "On: edit counts below (saved to PlayerPrefs via PlayerBattleRewardManager).\n"
                + "Off: show saved inventory; updated after battle victory."));

        using (new EditorGUI.DisabledScope(!_useManualBattleRewardsForDebug.boolValue))
        {
            EditorGUILayout.PropertyField(
                _manualSuperRareRewardCount,
                new GUIContent("SuperRare Rewards"));
            EditorGUILayout.PropertyField(_manualRareRewardCount, new GUIContent("Rare Rewards"));
            EditorGUILayout.PropertyField(_manualNormalRewardCount, new GUIContent("Normal Rewards"));
        }

        DrawBattleRewardsLivePreview();

        EditorGUILayout.Space(4f);
        EditorGUI.BeginChangeCheck();
        if (GUILayout.Button("Apply Battle Rewards"))
        {
            serializedObject.ApplyModifiedProperties();
            home.RefreshBattleRewardsBinding();
        }

        if (GUILayout.Button("Copy Saved Inventory To Manual"))
        {
            serializedObject.ApplyModifiedProperties();
            home.CopySavedBattleRewardsToManual();
            serializedObject.Update();
        }

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.Update();
        }
    }

    void DrawBattleRewardsLivePreview()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Resolved (read-only)", EditorStyles.miniBoldLabel);

        if (!Application.isPlaying)
        {
            if (!_useManualBattleRewardsForDebug.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to preview saved inventory, or enable Use Manual Battle Rewards.",
                    MessageType.Info);
            }

            return;
        }

        if (_useManualBattleRewardsForDebug.boolValue)
        {
            EditorGUILayout.IntField("SuperRare", _manualSuperRareRewardCount.intValue);
            EditorGUILayout.IntField("Rare", _manualRareRewardCount.intValue);
            EditorGUILayout.IntField("Normal", _manualNormalRewardCount.intValue);
            return;
        }

        if (PlayerBattleRewardManager.Instance == null)
        {
            EditorGUILayout.HelpBox("PlayerBattleRewardManager not ready.", MessageType.Warning);
            return;
        }

        var manager = PlayerBattleRewardManager.Instance;
        EditorGUILayout.IntField("SuperRare", manager.SuperRareCount);
        EditorGUILayout.IntField("Rare", manager.RareCount);
        EditorGUILayout.IntField("Normal", manager.NormalCount);
    }

    void DrawAttackHpPerLevelEditor()
    {
        var config = _playerLevelConfig.objectReferenceValue as PlayerLevelConfig;
        if (config == null)
        {
            EditorGUILayout.HelpBox(
                "Assign Player Level Config in the section above to edit per-level Attack and Max HP.",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Attack & Max HP Per Level (1–5)", EditorStyles.miniBoldLabel);

        var configObject = new SerializedObject(config);
        SerializedProperty statsTable = configObject.FindProperty("statsPerLevel");
        if (statsTable != null)
        {
            EditorGUILayout.PropertyField(statsTable, GUIContent.none, true);
        }

        configObject.ApplyModifiedProperties();
    }

    void DrawCombatStatsLivePreview(HomeSceneManager home)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Resolved (read-only)", EditorStyles.miniBoldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode to preview base stats, equipment bonuses, and final Attack / Max HP.",
                MessageType.Info);
            return;
        }

        bool savedOverride = _useManualCombatStatsForDebug.boolValue;
        if (savedOverride)
        {
            home.RefreshCombatStatsBinding();
        }

        PlayerCombatStatsResolver.ResolveEquipmentBonuses(out int bonusAttack, out int bonusMaxHp);
        int baseAttack = 0;
        int baseMaxHp = 1;
        if (PlayerLevelManager.Instance != null)
        {
            baseAttack = PlayerLevelManager.Instance.BaseAttack;
            baseMaxHp = PlayerLevelManager.Instance.BaseMaxHp;
        }

        PlayerCombatStats total = PlayerCombatStatsResolver.ResolveCurrent();
        EditorGUILayout.IntField("Base Attack", baseAttack);
        EditorGUILayout.IntField("Base Max HP", baseMaxHp);
        EditorGUILayout.IntField("Equipment Attack Bonus", bonusAttack);
        EditorGUILayout.IntField("Equipment HP Bonus", bonusMaxHp);
        EditorGUILayout.Space(2f);
        EditorGUILayout.IntField("Final Attack", total.Attack);
        EditorGUILayout.IntField("Final Max HP", total.MaxHp);

        if (PlayerCombatStatsResolver.IsUsingDebugOverride)
        {
            EditorGUILayout.HelpBox("Manual combat override is active.", MessageType.Info);
        }

        EditorGUILayout.Space(4f);
        if (GUILayout.Button("Copy Resolved → Manual"))
        {
            serializedObject.ApplyModifiedProperties();
            home.CopyResolvedCombatStatsToManual();
            serializedObject.Update();
        }
    }

    void DrawMaxExpPerLevelPreview()
    {
        var config = _playerLevelConfig.objectReferenceValue as PlayerLevelConfig;
        if (config == null || config.ExpPerLevel == null)
        {
            return;
        }

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Max Exp Per Level (from config)", EditorStyles.miniBoldLabel);
        using (new EditorGUI.IndentLevelScope())
        {
            for (int level = 1; level <= PlayerLevelManager.MaxLevel; level++)
            {
                int maxExp = config.GetExpRequiredForLevel(level);
                EditorGUILayout.LabelField($"Level {level}", maxExp.ToString());
            }
        }
    }

    void DrawSavedProgressPreview()
    {
        EditorGUILayout.LabelField("Saved Progress (read-only)", EditorStyles.miniBoldLabel);

        if (!Application.isPlaying || PlayerLevelManager.Instance == null)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode to preview saved level progress from PlayerLevelManager.",
                MessageType.Info);
            return;
        }

        PlayerLevelManager manager = PlayerLevelManager.Instance;
        EditorGUILayout.IntField("Level", manager.CurrentLevel);
        EditorGUILayout.IntField("Current Exp", manager.CurrentLevelExp);
        EditorGUILayout.IntField("Required Exp", manager.GetExpRequiredForCurrentLevel());
        EditorGUILayout.Toggle("Is Max Level", manager.IsMaxLevel);

        EditorGUILayout.Space(4f);
        if (GUILayout.Button("Reset Saved Level Progress"))
        {
            manager.ResetProgress();
        }
    }
}
#endif
