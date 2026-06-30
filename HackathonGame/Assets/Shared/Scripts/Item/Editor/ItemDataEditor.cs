#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemData))]
public class ItemDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject, "m_Script", "cdBgm", "bgmClip");

        SerializedProperty cdBgm = serializedObject.FindProperty("cdBgm");
        if (cdBgm == null)
        {
            cdBgm = serializedObject.FindProperty("bgmClip");
        }

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("BGM（CD 装備時）", EditorStyles.boldLabel);

        if (cdBgm != null)
        {
            EditorGUILayout.PropertyField(
                cdBgm,
                new GUIContent("BGM", "装備中にシーンをまたいでループ再生する曲"));
        }
        else
        {
            EditorGUILayout.HelpBox(
                "BGM フィールド (cdBgm) が見つかりません。Console のコンパイルエラーを確認してください。",
                MessageType.Warning);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
