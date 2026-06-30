using UnityEngine;

//ゲーム開始時に初期装備を着用
[CreateAssetMenu(fileName = "StarterInventoryConfig", menuName = "Inventory/Starter Inventory")]
public class StarterInventoryConfig : ScriptableObject
{
    public ItemData starterTop;
    public ItemData starterBottom;
    public ItemData starterWeapon;
    public ItemData starterCD;

    [Header("Upgrade variants (Craft / not in initial inventory)")]
    public ItemData betterTop;
    public ItemData betterBottom;
    public ItemData betterCD;
}
