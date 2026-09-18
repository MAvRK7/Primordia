using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Attach to your shop menu panel. Rebuilds the item list whenever
// ShopManager says the shop has refreshed (opened, or after a purchase).
public class ShopUI : MonoBehaviour
{
    [Header("References")]
    public ShopManager shopManager;
    public Transform contentParent;   // the panel that holds the rows (e.g. a Vertical Layout Group)
    public ShopItemUI itemUIPrefab;   // your ShopItemUI prefab

    [Header("Pages")]
    [Min(1)] public int itemsPerPage = 4;
    public Button previousPageButton;
    public Button nextPageButton;
    public TMP_Text pageText;
    public TMP_Text resourceText;

    int page;

    private void OnEnable()
    {
        if (shopManager != null)
        {
            shopManager.OnShopRefreshed += HandleShopRefreshed;
            HandleShopRefreshed(shopManager.shopItems);
        }
        if (previousPageButton != null) previousPageButton.onClick.AddListener(PreviousPage);
        if (nextPageButton != null) nextPageButton.onClick.AddListener(NextPage);
    }

    private void OnDisable()
    {
        if (shopManager != null)
        {
            shopManager.OnShopRefreshed -= HandleShopRefreshed;
        }
        if (previousPageButton != null) previousPageButton.onClick.RemoveListener(PreviousPage);
        if (nextPageButton != null) nextPageButton.onClick.RemoveListener(NextPage);
    }

    private void HandleShopRefreshed(ShopItem[] items)
    {
        RebuildList(items);
    }

    void PreviousPage() => ChangePage(-1);
    void NextPage() => ChangePage(1);

    void ChangePage(int direction)
    {
        page += direction;
        if (shopManager != null) RebuildList(shopManager.shopItems);
    }

    private void RebuildList(ShopItem[] items)
    {
        if (contentParent == null || itemUIPrefab == null) return;

        foreach (Transform child in contentParent)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        int count = items != null ? items.Length : 0;
        int pageSize = Mathf.Max(1, itemsPerPage);
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(count / (float)pageSize));
        page = Mathf.Clamp(page, 0, pageCount - 1);
        if (previousPageButton != null) previousPageButton.interactable = page > 0;
        if (nextPageButton != null) nextPageButton.interactable = page < pageCount - 1;
        if (pageText != null) pageText.text = $"{page + 1} / {pageCount}";
        if (resourceText != null && shopManager != null)
        {
            var sack = shopManager.resourceSack;
            resourceText.text = shopManager.WaitingForPickup
                ? "Pick up your weapon from the counter to buy again."
                : sack != null ? $"Sack: {sack.GetMeat()} Meat / {sack.GetBones()} Bones"
                : "Resource sack unavailable.";
        }

        for (int i = page * pageSize; i < Mathf.Min(count, (page + 1) * pageSize); i++)
        {
            ShopItemUI row = Instantiate(itemUIPrefab, contentParent);
            row.Bind(items[i], shopManager);
        }
    }
}
