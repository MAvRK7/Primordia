using UnityEngine;

public class ResourceSack : MonoBehaviour
{
    [Header("Dinosaur Parts")]
    public int meat = 25;
    public int bones = 10;

    public event System.Action ResourcesChanged;

    public void AddResources(int meatAmount, int boneAmount)
    {
        meat += Mathf.Max(0, meatAmount);
        bones += Mathf.Max(0, boneAmount);
        ResourcesChanged?.Invoke();
    }

    public bool IsPlacedOnShopTable { get; private set; }

    public void SetPlacedOnShopTable(bool placed)
    {
        IsPlacedOnShopTable = placed;

        Debug.Log(
            placed
                ? "Resource sack placed on shop table."
                : "Resource sack removed from shop table."
        );
    }

    public int GetMeat()
    {
        return meat;
    }

    public int GetBones()
    {
        return bones;
    }

    public bool CanAfford(int meatCost, int boneCost)
    {
        return meat >= meatCost && bones >= boneCost;
    }

    public bool SpendResources(int meatCost, int boneCost)
    {
        if (!CanAfford(meatCost, boneCost))
        {
            return false;
        }

        meat -= meatCost;
        bones -= boneCost;
        ResourcesChanged?.Invoke();

        Debug.Log(
            $"Spent {meatCost} meat and {boneCost} bones."
        );

        return true;
    }
}
