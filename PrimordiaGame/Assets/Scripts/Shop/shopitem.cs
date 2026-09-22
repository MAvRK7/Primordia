using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class ShopItem
{
    [Header("Identity")]
    public string itemId;
    public string itemName;

    [TextArea]
    public string description;

    public GameObject weaponPrefab;

    [Header("Required Dinosaur Parts")]
    [FormerlySerializedAs("requiredMeat")]
    public int meatCost;
    [FormerlySerializedAs("requiredBones")]
    public int boneCost;

    [Header("Visuals")]
    public Sprite icon; // used by the UI row

    public bool CanBePurchased(int availableMeat, int availableBones)
    {
        return availableMeat >= meatCost && availableBones >= boneCost;
    }
}
