using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ShopManager : MonoBehaviour
{
    [Header("Shop")]
    public ShopItem[] shopItems;

    [Header("Player Resources")]
    public ResourceSack resourceSack;

    [Header("Counter Delivery")]
    public Transform weaponSpawnPoint;

    XRGrabInteractable deliveredWeapon;
    public bool WaitingForPickup { get; private set; }

    // UI rows and 3D weapon displays subscribe to these instead of polling every frame.
    public event Action<ShopItem[]> OnShopRefreshed;
    public event Action<ShopItem> OnItemPurchased;
    public event Action<ShopItem> OnPurchaseFailed;

    void OnEnable()
    {
        if (resourceSack != null) resourceSack.ResourcesChanged += CheckAvailableWeapons;
    }

    void OnDisable()
    {
        if (resourceSack != null) resourceSack.ResourcesChanged -= CheckAvailableWeapons;
    }

    void Update()
    {
        if (WaitingForPickup && (deliveredWeapon == null || deliveredWeapon.isSelected))
        {
            WaitingForPickup = false;
            deliveredWeapon = null;
            CheckAvailableWeapons();
        }
    }

    public bool CanBuy(ShopItem item)
    {
        return item != null && shopItems != null && Array.IndexOf(shopItems, item) >= 0 &&
            resourceSack != null && weaponSpawnPoint != null && !WaitingForPickup &&
            item.weaponPrefab != null && item.weaponPrefab.GetComponent<XRGrabInteractable>() != null &&
            item.meatCost >= 0 && item.boneCost >= 0 && (item.meatCost > 0 || item.boneCost > 0) &&
            resourceSack.CanAfford(item.meatCost, item.boneCost);
    }

    public bool BuyItem(ShopItem item)
    {
        if (!CanBuy(item))
        {
            OnPurchaseFailed?.Invoke(item);
            return false;
        }

        // Deliver before charging, so a missing/broken prefab cannot consume resources.
        var weapon = Instantiate(item.weaponPrefab, weaponSpawnPoint.position, weaponSpawnPoint.rotation);
        SceneManager.MoveGameObjectToScene(weapon, gameObject.scene);
        var grab = weapon.GetComponent<XRGrabInteractable>();
        Physics.SyncTransforms();
        Bounds? bounds = null;
        foreach (var collider in weapon.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger) continue;
            var combined = bounds ?? collider.bounds;
            combined.Encapsulate(collider.bounds);
            bounds = combined;
        }
        if (grab == null || !bounds.HasValue)
        {
            weapon.SetActive(false);
            Destroy(weapon);
            OnPurchaseFailed?.Invoke(item);
            return false;
        }
        // Align the physical bottom with the counter, regardless of the prefab's pivot.
        var footprint = bounds.Value;
        weapon.transform.position += weaponSpawnPoint.position -
            new Vector3(footprint.center.x, footprint.min.y, footprint.center.z);

        deliveredWeapon = grab;
        WaitingForPickup = true;
        if (!resourceSack.SpendResources(item.meatCost, item.boneCost))
        {
            deliveredWeapon = null;
            WaitingForPickup = false;
            weapon.SetActive(false);
            Destroy(weapon);
            OnPurchaseFailed?.Invoke(item);
            return false;
        }

        OnItemPurchased?.Invoke(item);
        CheckAvailableWeapons();
        return true;
    }

    // Called by Shopkeeper.OpenShop() and after every purchase.
    // Anything showing item state (UI rows, shelf models) listens for this.
    public void CheckAvailableWeapons()
    {
        OnShopRefreshed?.Invoke(shopItems);
    }
}
