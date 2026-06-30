#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// SkillDataSO の旧 QTE フィールドを Qte Variants 配列へ移行する Editor メニュー。
/// </summary>
public static class SkillDataSOMigration
{
    [MenuItem("InGame/Migrate Skill QTE Variants")]
    private static void MigrateAllSkillData()
    {
        string[] guids = AssetDatabase.FindAssets("t:SkillDataSO");
        int migrated = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            SkillDataSO skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(path);
            if (skill == null)
            {
                continue;
            }

            skill.MigrateLegacyQteFieldsIfNeeded();
            skill.MigrateVariantFieldsIfNeeded();
            EditorUtility.SetDirty(skill);
            migrated++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[SkillDataSOMigration] Processed {migrated} SkillDataSO asset(s).");
    }
}
#endif
