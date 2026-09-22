using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Attach to your shop-row prefab: an Image (icon), two TMP texts, and a Button.
public class ShopItemUI : MonoBehaviour
{
    [Header("References")]
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text costText;
    public Button buyButton;

    [Header("Locked / Unlocked Look")]
    public Color unlockedTint = Color.white;
    public Color lockedTint = new Color(0.25f, 0.25f, 0.25f, 1f);

    private ShopItem boundItem;
    private ShopManager shopManager;

    public void Bind(ShopItem item, ShopManager manager)
    {
        boundItem = item;
        shopManager = manager;

        if (nameText != null) nameText.text = item.itemName;
        if (costText != null) costText.text = $"{item.meatCost} Meat & {item.boneCost} Bones";
        if (icon != null && item.icon != null) icon.sprite = item.icon;

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(TryBuy);
        }

        Refresh();
    }

    public void Refresh()
    {
        if (boundItem == null || shopManager == null) return;

        bool canAfford = shopManager.CanBuy(boundItem);

        if (icon != null)
        {
            icon.color = canAfford ? unlockedTint : lockedTint;
        }

        if (buyButton != null)
        {
            buyButton.interactable = canAfford;
        }
    }

    private void TryBuy()
    {
        if (shopManager == null || boundItem == null) return;
        shopManager.BuyItem(boundItem);
    }
}