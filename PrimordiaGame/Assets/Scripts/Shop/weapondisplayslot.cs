using System;
using UnityEngine;

// Optional: attach to a physical weapon model on the shop shelf if you want
// the actual 3D weapon to darken/brighten instead of (or alongside) the UI.
// Set shopItemIndex to match the item's position in ShopManager.shopItems.
public class WeaponDisplaySlot : MonoBehaviour
{
    [Header("Match this display to a shop item by index")]
    public int shopItemIndex;

    [Header("References")]
    public ShopManager shopManager;
    public Renderer weaponRenderer;
    public Material lockedMaterial;   // a dark / desaturated material
    public Material unlockedMaterial; // the weapon's real material

    private Action<ShopItem[]> refreshHandler;

    private void OnEnable()
    {
        if (shopManager == null) return;

        refreshHandler = _ => Refresh();
        shopManager.OnShopRefreshed += refreshHandler;
        Refresh();
    }

    private void OnDisable()
    {
        if (shopManager != null && refreshHandler != null)
        {
            shopManager.OnShopRefreshed -= refreshHandler;
        }
    }

    public void Refresh()
    {
        if (shopManager == null || weaponRenderer == null) return;
        if (shopManager.shopItems == null) return;
        if (shopItemIndex < 0 || shopItemIndex >= shopManager.shopItems.Length) return;

        ShopItem item = shopManager.shopItems[shopItemIndex];
        bool canAfford = shopManager.CanBuy(item);

        weaponRenderer.sharedMaterial = canAfford ? unlockedMaterial : lockedMaterial;
    }
}