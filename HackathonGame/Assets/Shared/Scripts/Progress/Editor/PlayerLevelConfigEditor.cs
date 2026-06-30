#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerLevelConfig))]
public sealed class PlayerLevelConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Max Exp Per Level (1–5)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Each value is the exp needed while at that level to advance (or display cap at Lv 5).",
            MessageType.None);

        SerializedProperty expTable = serializedObject.FindProperty("expPerLevel");
        if (expTable != null)
        {
            EditorGUILayout.PropertyField(expTable, GUIContent.none, true);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Attack & Max HP Per Level (1–5)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Base attack and max HP at each level. Equipment bonuses are added separately at runtime.",
            MessageType.None);

        SerializedProperty statsTable = serializedObject.FindProperty("statsPerLevel");
        if (statsTable != null)
        {
            EditorGUILayout.PropertyField(statsTable, GUIContent.none, true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}

public static class PlayerLevelConfigMenu
{
    const string ResourcesPath = "Assets/Shared/Resources";
    const string AssetPath = ResourcesPath + "/PlayerLevelConfig.asset";

    [MenuItem("Assets/Create/Progress/Player Level Config (Resources)")]
    public static void CreatePlayerLevelConfigInResources()
    {
        if (!Directory.Exists(ResourcesPath))
        {
            Directory.CreateDirectory(ResourcesPath);
        }

        var existing = AssetDatabase.LoadAssetAtPath<PlayerLevelConfig>(AssetPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            return;
        }

        var config = ScriptableObject.CreateInstance<PlayerLevelConfig>();
        AssetDatabase.CreateAsset(config, AssetPath);
        AssetDatabase.SaveAssets();
        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);
    }
}
#endif
