#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MainScene に QTE 用 HUD スライド View を追加する。
/// </summary>
public static class BattleHudSlideSceneSetup
{
    private const string MenuPath = "InGame/Setup Battle HUD Slide";

    [MenuItem(MenuPath)]
    public static void SetupBattleHudSlide()
    {
        BattleHudSlideView slideView = Object.FindFirstObjectByType<BattleHudSlideView>(FindObjectsInactive.Include);
        if (slideView == null)
        {
            InGameManager managerForParent = Object.FindFirstObjectByType<InGameManager>(FindObjectsInactive.Include);
            GameObject host = managerForParent != null ? managerForParent.gameObject : new GameObject("BattleHudSlide");
            if (managerForParent == null)
            {
                Undo.RegisterCreatedObjectUndo(host, "Create BattleHudSlide");
            }

            slideView = Undo.AddComponent<BattleHudSlideView>(host);
        }

        RectTransform playerHpRoot = FindRectTransformByName("HPSlider")
            ?? FindRectTransformByName("Slider")
            ?? FindRectTransformByName("PlayerHPSlider");
        RectTransform[] skillRoots = FindSkillButtonRoots();

        SerializedObject serializedSlide = new SerializedObject(slideView);
        SerializedProperty hpProp = serializedSlide.FindProperty("_playerHpRoot");
        if (hpProp != null)
        {
            hpProp.objectReferenceValue = playerHpRoot;
        }

        SerializedProperty skillRootsProp = serializedSlide.FindProperty("_skillButtonRoots");
        if (skillRootsProp != null)
        {
            skillRootsProp.arraySize = skillRoots.Length;
            for (int i = 0; i < skillRoots.Length; i++)
            {
                skillRootsProp.GetArrayElementAtIndex(i).objectReferenceValue = skillRoots[i];
            }
        }

        serializedSlide.ApplyModifiedPropertiesWithoutUndo();

        InGameManager manager = Object.FindFirstObjectByType<InGameManager>(FindObjectsInactive.Include);
        if (manager != null)
        {
            SerializedObject serializedManager = new SerializedObject(manager);
            SerializedProperty slideProp = serializedManager.FindProperty("_battleHudSlide");
            if (slideProp != null)
            {
                slideProp.objectReferenceValue = slideView;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[BattleHudSlideSceneSetup] InGameManager._battleHudSlide が見つかりません。");
            }
        }
        else
        {
            Debug.LogWarning("[BattleHudSlideSceneSetup] InGameManager が見つかりません。");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[BattleHudSlideSceneSetup] HUD スライドのセットアップが完了しました。", slideView);
    }

    private static RectTransform FindRectTransformByName(string objectName)
    {
        RectTransform[] rects = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i].name == objectName)
            {
                return rects[i];
            }
        }

        return null;
    }

    private static RectTransform[] FindSkillButtonRoots()
    {
        CommandPanelView commandPanel = Object.FindFirstObjectByType<CommandPanelView>(FindObjectsInactive.Include);
        if (commandPanel == null)
        {
            return new RectTransform[0];
        }

        SerializedObject serializedPanel = new SerializedObject(commandPanel);
        SerializedProperty buttonsProp = serializedPanel.FindProperty("_skillButtons");
        if (buttonsProp == null || !buttonsProp.isArray)
        {
            return new RectTransform[0];
        }

        RectTransform[] roots = new RectTransform[buttonsProp.arraySize];
        for (int i = 0; i < buttonsProp.arraySize; i++)
        {
            Button button = buttonsProp.GetArrayElementAtIndex(i).objectReferenceValue as Button;
            roots[i] = button != null ? button.transform as RectTransform : null;
        }

        return roots;
    }
}
#endif
