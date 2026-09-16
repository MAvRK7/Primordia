/*using UnityEngine;

public class ShopManager : MonoBehaviour
{
    [Header("Shop")]
    public ShopItem[] shopItems;

    [Header("Player Resources")]
    public ResourceSack resourceSack;

    public bool CanBuy(ShopItem item)
    {
        if (resourceSack == null)
        {
            Debug.LogWarning("Resource Sack has not been assigned.");
            return false;
        }

        return resourceSack.CanAfford(
            item.meatCost,
            item.boneCost
        );
    }

    public bool BuyItem(ShopItem item)
    {
        if (resourceSack == null)
        {
            Debug.LogWarning("Resource Sack has not been assigned.");
            return false;
        }

        if (!CanBuy(item))
        {
            Debug.Log("Not enough resources to buy " + item.itemName);
            return false;
        }

        bool purchaseSuccessful = resourceSack.SpendResources(
            item.meatCost,
            item.boneCost
        );

        if (purchaseSuccessful)
        {
            Debug.Log("Purchased: " + item.itemName);
            return true;
        }

        return false;
    }
}
*/
using System;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    [Header("Shop")]
    public ShopItem[] shopItems;

    [Header("Player Resources")]
    public ResourceSack resourceSack;

    // UI rows and 3D weapon displays subscribe to these instead of polling every frame.
    public event Action<ShopItem[]> OnShopRefreshed;
    public event Action<ShopItem> OnItemPurchased;
    public event Action<ShopItem> OnPurchaseFailed;

    public bool CanBuy(ShopItem item)
    {
        if (resourceSack == null)
        {
            Debug.LogWarning("Resource Sack has not been assigned.");
            return false;
        }

        return resourceSack.CanAfford(item.meatCost, item.boneCost);
    }

    public bool BuyItem(ShopItem item)
    {
        if (resourceSack == null)
        {
            Debug.LogWarning("Resource Sack has not been assigned.");
            return false;
        }

        if (!CanBuy(item))
        {
            Debug.Log("Not enough resources to buy " + item.itemName);
            OnPurchaseFailed?.Invoke(item);
            return false;
        }

        bool purchaseSuccessful = resourceSack.SpendResources(item.meatCost, item.boneCost);

        if (purchaseSuccessful)
        {
            Debug.Log("Purchased: " + item.itemName);
            OnItemPurchased?.Invoke(item);
            CheckAvailableWeapons(); // affordability may have changed for other items too
            return true;
        }

        OnPurchaseFailed?.Invoke(item);
        return false;
    }

    // Called by Shopkeeper.OpenShop() and after every purchase.
    // Anything showing item state (UI rows, shelf models) listens for this.
    public void CheckAvailableWeapons()
    {
        OnShopRefreshed?.Invoke(shopItems);
    }
}