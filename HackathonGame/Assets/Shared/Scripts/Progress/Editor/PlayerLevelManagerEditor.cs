#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerLevelManager))]
public sealed class PlayerLevelManagerEditor : Editor
{
    SerializedProperty _config;
    SerializedProperty _expPerLevel;
    SerializedProperty _statsPerLevel;
    SerializedProperty _currentLevel;
    SerializedProperty _currentLevelExp;

    void OnEnable()
    {
        _config = serializedObject.FindProperty("config");
        _expPerLevel = serializedObject.FindProperty("expPerLevel");
        _statsPerLevel = serializedObject.FindProperty("statsPerLevel");
        _currentLevel = serializedObject.FindProperty("currentLevel");
        _currentLevelExp = serializedObject.FindProperty("currentLevelExp");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_config);

        bool usingConfig = _config.objectReferenceValue != null;
        if (usingConfig)
        {
            EditorGUILayout.HelpBox(
                "Exp and base stats per level are read from the assigned Player Level Config asset.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.LabelField("Exp Per Level (Lv 1–5)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_expPerLevel, GUIContent.none, true);
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Attack & Max HP Per Level (Lv 1–5)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_statsPerLevel, GUIContent.none, true);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Saved Progress", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_currentLevel);
        EditorGUILayout.PropertyField(_currentLevelExp);

        if (Application.isPlaying)
        {
            PlayerLevelManager manager = (PlayerLevelManager)target;
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Runtime Preview", EditorStyles.boldLabel);
            EditorGUILayout.IntField("Level", manager.CurrentLevel);
            EditorGUILayout.IntField("Current Exp", manager.CurrentLevelExp);
            EditorGUILayout.IntField("Required Exp", manager.GetExpRequiredForCurrentLevel());
            EditorGUILayout.IntField("Base Attack", manager.BaseAttack);
            EditorGUILayout.IntField("Base Max HP", manager.BaseMaxHp);

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("Reset Progress"))
            {
                manager.ResetProgress();
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
