using UnityEngine;

// Attach to your shop menu panel. Rebuilds the item list whenever
// ShopManager says the shop has refreshed (opened, or after a purchase).
public class ShopUI : MonoBehaviour
{
    [Header("References")]
    public ShopManager shopManager;
    public Transform contentParent;   // the panel that holds the rows (e.g. a Vertical Layout Group)
    public ShopItemUI itemUIPrefab;   // your ShopItemUI prefab

    private void OnEnable()
    {
        if (shopManager != null)
        {
            shopManager.OnShopRefreshed += HandleShopRefreshed;
        }
    }

    private void OnDisable()
    {
        if (shopManager != null)
        {
            shopManager.OnShopRefreshed -= HandleShopRefreshed;
        }
    }

    private void HandleShopRefreshed(ShopItem[] items)
    {
        RebuildList(items);
    }

    private void RebuildList(ShopItem[] items)
    {
        if (contentParent == null || itemUIPrefab == null) return;

        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        if (items == null) return;

        foreach (ShopItem item in items)
        {
            ShopItemUI row = Instantiate(itemUIPrefab, contentParent);
            row.Bind(item, shopManager);
        }
    }
}