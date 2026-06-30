using UnityEngine;
using UnityEngine.Serialization;

// アイテムのカテゴリーを定義する列挙型（Enum）
public enum ItemType
{
    Top,     // 上半身の服
    Bottom,  // 下半身の服
    Weapon,  // 武器
    CD       // レコード（CD）
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    [Header("アウトゲーム用データ")]
    public string itemId;          // 固有のID（例: "weapon_001"）
    public string itemName;        // 画面に表示するアイテム名
    public ItemType itemType;      // アイテムの種類
    public Sprite itemIcon;        // UIに表示するアイコン画像
    [TextArea]
    public string description;     // アイテムの説明文

    [Header("インゲーム用データ")]
    public float attackPower;      // 攻撃力（武器用）
    public float defense;          // 防御力（服用）

    [Header("BGM（CD 装備時）")]
    [Tooltip("この CD を装備している間、シーンをまたいで再生するループ BGM。")]
    [FormerlySerializedAs("bgmClip")]
    public AudioClip cdBgm;
}