/*using UnityEngine;

[System.Serializable]
public class ShopItem
{
    [Header("Weapon")]
    public string itemName;

    [Header("Required Dinosaur Parts")]
    public int requiredMeat;
    public int requiredBones;

    public bool CanBePurchased(int availableMeat, int availableBones)
    {
        return availableMeat >= requiredMeat &&
               availableBones >= requiredBones;
    }
}
*/

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