using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>Wires the existing campsite shop to the weapon catalog and counter delivery.</summary>
public static class PrimordiaShopSetup
{
    const string ShopPath = "Assets/Prefabs/Campsite Shop.prefab";
    const string RowPath = "Assets/Prefabs/ShopItemRow.prefab";

    [MenuItem("Tools/Primordia/Shop/Set Up Demo Shop")]
    public static void SetUpDemoShop()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before setting up the shop.");

        var items = AssetDatabase.FindAssets("t:WeaponDefinition", new[] { "Assets/Data/Weapons" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .OrderBy(definition => definition.Type).ThenBy(definition => definition.Tier)
            .ThenBy(definition => definition.DisplayName).Select(CreateItem).ToArray();
        if (items.Length == 0)
            throw new InvalidOperationException("No weapon definitions found.");

        ConfigureRow();
        var root = PrefabUtility.LoadPrefabContents(ShopPath);
        try
        {
            var manager = root.GetComponentInChildren<ShopManager>(true);
            if (manager == null || manager.resourceSack == null)
                throw new InvalidOperationException("The campsite shop needs its existing resource sack.");
            manager.shopItems = items;

            var counter = root.transform.Find("Counter1").GetComponent<BoxCollider>();
            var pickup = root.transform.Find("Weapon Pickup");
            if (pickup == null)
            {
                pickup = new GameObject("Weapon Pickup").transform;
                pickup.SetParent(root.transform, false);
            }
            var top = counter.bounds;
            pickup.position = new Vector3(top.center.x, top.max.y + 0.03f, top.center.z);
            // Lay each weapon along the six-metre counter, on its side.
            pickup.localRotation = Quaternion.Euler(0f, 90f, 90f);
            manager.weaponSpawnPoint = pickup;

            ConfigureMenu(root.GetComponentInChildren<ShopUI>(true));
            var dialogue = root.GetComponentInChildren<ShopkeeperDialogue>(true);
            if (dialogue != null)
                dialogue.purchaseSuccessLines = new[] { "Ready on the counter. Pick it up!" };

            if (PrefabUtility.SaveAsPrefabAsset(root, ShopPath) == null)
                throw new InvalidOperationException("Could not save the campsite shop.");
            Debug.Log($"Demo shop ready: {items.Length} weapons, costs of 1–2 meat and bones, counter pickup at {pickup.localPosition}. Existing sack balances preserved.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static ShopItem CreateItem(WeaponDefinition definition)
    {
        string path = definition.DisplayName == "Knife" ? "Assets/Prefabs/Knife.prefab"
            : $"Assets/Prefabs/Weapons/{definition.DisplayName}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null || prefab.GetComponent<XRGrabInteractable>() == null ||
            prefab.GetComponent<Rigidbody>() == null ||
            !prefab.GetComponentsInChildren<Collider>().Any(collider => collider.enabled && !collider.isTrigger))
            throw new InvalidOperationException($"{definition.DisplayName} needs a grabbable weapon prefab with a physical collider.");

        return new ShopItem
        {
            itemId = definition.WeaponId,
            itemName = definition.DisplayName,
            weaponPrefab = prefab,
            meatCost = definition.Type == WeaponType.Gun ? 2 : 1,
            boneCost = definition.Tier >= 4 ? 2 : 1
        };
    }

    static void ConfigureRow()
    {
        var root = PrefabUtility.LoadPrefabContents(RowPath);
        try
        {
            var row = root.GetComponent<ShopItemUI>();
            ((RectTransform)root.transform).sizeDelta = new Vector2(760f, 64f);
            SetHeight(root, 64f);
            FormatText(row.nameText, 27f);
            FormatText(row.costText, 24f);
            PlaceText(row.nameText.rectTransform, new Vector2(0f, 0f), new Vector2(0.62f, 1f));
            PlaceText(row.costText.rectTransform, new Vector2(0.62f, 0f), Vector2.one);
            row.costText.alignment = TextAlignmentOptions.MidlineRight;
            row.unlockedTint = new Color(0.19f, 0.29f, 0.24f);
            row.lockedTint = new Color(0.10f, 0.12f, 0.11f);
            row.buyButton.navigation = new Navigation { mode = Navigation.Mode.None };
            if (PrefabUtility.SaveAsPrefabAsset(root, RowPath) == null)
                throw new InvalidOperationException("Could not save the shop row.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ConfigureMenu(ShopUI ui)
    {
        if (ui == null) throw new InvalidOperationException("The campsite shop needs its menu.");
        var panel = (RectTransform)ui.transform;
        panel.sizeDelta = new Vector2(800f, 560f);
        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.spacing = 8f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        panel.GetComponent<Image>().color = new Color(0.035f, 0.055f, 0.045f, 0.97f);

        var title = panel.Find("Title").GetComponent<TMP_Text>();
        title.text = "WEAPON SHOP";
        FormatText(title, 34f);
        SetHeight(title.gameObject, 44f);
        var instructions = panel.Find("Instructions").GetComponent<TMP_Text>();
        instructions.text = "Select a weapon to buy with your sack's resources.\nCollect your purchase from the counter.";
        FormatText(instructions, 24f);
        SetHeight(instructions.gameObject, 54f);

        // These two children are owned by this setup command; rebuilding is idempotent.
        foreach (string name in new[] { "Shop Balance", "Shop Paging" })
        {
            var existing = panel.Find(name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }
        ui.resourceText = CreateText(panel, "Shop Balance", title.font);
        ui.resourceText.text = "Sack resources";
        ui.resourceText.transform.SetSiblingIndex(2);
        SetHeight(ui.resourceText.gameObject, 42f);

        var fitter = ui.contentParent.GetComponent<ContentSizeFitter>();
        if (fitter != null) UnityEngine.Object.DestroyImmediate(fitter);
        var rows = ui.contentParent.GetComponent<VerticalLayoutGroup>();
        rows.spacing = 8f;
        rows.childControlWidth = rows.childControlHeight = true;
        rows.childForceExpandWidth = true;
        rows.childForceExpandHeight = false;
        SetHeight(ui.contentParent.gameObject, 280f);
        ui.itemsPerPage = 4;

        var footer = new GameObject("Shop Paging", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        footer.layer = 5;
        footer.transform.SetParent(panel, false);
        SetHeight(footer, 48f);
        var footerLayout = footer.GetComponent<HorizontalLayoutGroup>();
        footerLayout.spacing = 8f;
        footerLayout.childControlWidth = footerLayout.childControlHeight = true;
        footerLayout.childForceExpandWidth = false;
        footerLayout.childForceExpandHeight = true;
        ui.previousPageButton = CreateButton(footer.transform, "Previous", title.font);
        ui.pageText = CreateText(footer.transform, "Page", title.font);
        ui.pageText.alignment = TextAlignmentOptions.Center;
        ui.pageText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        ui.nextPageButton = CreateButton(footer.transform, "Next", title.font);
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
    }

    static void SetHeight(GameObject target, float height)
    {
        var layout = target.GetComponent<LayoutElement>();
        if (layout == null) layout = target.AddComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = height;
        layout.flexibleHeight = 0f;
    }

    static TMP_Text CreateText(Transform parent, string name, TMP_FontAsset font)
    {
        var target = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        target.layer = 5;
        target.transform.SetParent(parent, false);
        var text = target.GetComponent<TMP_Text>();
        text.font = font;
        FormatText(text, 24f);
        return text;
    }

    static Button CreateButton(Transform parent, string label, TMP_FontAsset font)
    {
        var target = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        target.layer = 5;
        target.transform.SetParent(parent, false);
        target.GetComponent<LayoutElement>().preferredWidth = 180f;
        var background = target.GetComponent<Image>();
        background.color = new Color(0.19f, 0.29f, 0.24f);
        var button = target.GetComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var text = CreateText(target.transform, "Label", font);
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        PlaceText(text.rectTransform, Vector2.zero, Vector2.one);
        return button;
    }

    static void FormatText(TMP_Text text, float size)
    {
        text.fontSize = size;
        text.enableAutoSizing = false;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
    }

    static void PlaceText(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = new Vector2(16f, 4f);
        rect.offsetMax = new Vector2(-16f, -4f);
    }
}
