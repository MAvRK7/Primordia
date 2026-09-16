/*using UnityEngine;
using UnityEngine.InputSystem;

public class Shopkeeper : MonoBehaviour
{
    [Header("Shop")]
    public ShopManager shopManager;

    [Header("Temporary Testing UI")]
    public GameObject shopMenu;

    private bool shopOpen = false;

    private void Start()
    {
        if (shopMenu != null)
        {
            shopMenu.SetActive(false);
        }
    }

    private void Update()
    {
        // Temporary keyboard testing.
        // This will eventually be replaced with XR interaction.
        if (Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            ToggleShop();
        }
    }

    public void OpenShop()
    {
        shopOpen = true;

        if (shopMenu != null)
        {
            shopMenu.SetActive(true);
        }

        if (shopManager != null)
        {
            shopManager.CheckAvailableWeapons();
        }

        Debug.Log("Shopkeeper: What do you want to buy?");
    }

    public void CloseShop()
    {
        shopOpen = false;

        if (shopMenu != null)
        {
            shopMenu.SetActive(false);
        }

        Debug.Log("Shopkeeper: Come again.");
    }

    public void ToggleShop()
    {
        if (shopOpen)
        {
            CloseShop();
        }
        else
        {
            OpenShop();
        }
    }
}
*/
using UnityEngine;
using UnityEngine.InputSystem;

public class Shopkeeper : MonoBehaviour
{
    [Header("Shop")]
    public ShopManager shopManager;
    public ShopkeeperDialogue dialogue; // optional, but wires greetings/reactions if assigned

    [Header("Campsite Interaction")]
    public bool openWhenNearby;
    [Min(0.5f)] public float interactionDistance = 3f;

    private Camera playerCamera;

    [Header("Temporary Testing UI")]
    public GameObject shopMenu;

    private bool shopOpen = false;

    private void Start()
    {
        if (shopMenu != null)
        {
            shopMenu.SetActive(false);
        }

        if (shopManager != null)
        {
            shopManager.OnItemPurchased += HandlePurchaseSuccess;
            shopManager.OnPurchaseFailed += HandlePurchaseFailed;
        }
    }

    private void OnDestroy()
    {
        if (shopManager != null)
        {
            shopManager.OnItemPurchased -= HandlePurchaseSuccess;
            shopManager.OnPurchaseFailed -= HandlePurchaseFailed;
        }
    }

    private void Update()
    {
        // The campsite rig is spawned at runtime. Find its active camera after
        // spawning, so approaching the counter also works without a keyboard.
        if (openWhenNearby)
        {
            if (playerCamera == null || !playerCamera.isActiveAndEnabled)
                playerCamera = Camera.main;

            if (playerCamera == null)
            {
                if (shopOpen) CloseShop();
                return;
            }

            var counter = shopMenu != null ? shopMenu.transform : transform;
            var offset = playerCamera.transform.position - counter.position;
            var heightDifference = Mathf.Abs(offset.y);
            offset.y = 0f;
            // A wider exit radius prevents the menu flickering at the boundary.
            var radius = interactionDistance + (shopOpen ? 1f : 0f);
            var nearby = heightDifference < 3f && offset.sqrMagnitude <= radius * radius;
            if (nearby && !shopOpen) OpenShop();
            else if (!nearby && shopOpen) CloseShop();
            return;
        }

        // Temporary keyboard testing.
        // This will eventually be replaced with XR interaction.
        if (Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            ToggleShop();
        }
    }

    public void OpenShop()
    {
        shopOpen = true;

        if (shopMenu != null)
        {
            shopMenu.SetActive(true);
        }

        if (shopManager != null)
        {
            shopManager.CheckAvailableWeapons();
        }

        if (dialogue != null)
        {
            dialogue.SayGreeting();
        }

        Debug.Log("Shopkeeper: What do you want to buy?");
    }

    public void CloseShop()
    {
        shopOpen = false;

        if (shopMenu != null)
        {
            shopMenu.SetActive(false);
        }

        Debug.Log("Shopkeeper: Come again.");
    }

    public void ToggleShop()
    {
        if (shopOpen)
        {
            CloseShop();
        }
        else
        {
            OpenShop();
        }
    }

    private void HandlePurchaseSuccess(ShopItem item)
    {
        if (dialogue != null) dialogue.SayPurchaseSuccess();
    }

    private void HandlePurchaseFailed(ShopItem item)
    {
        if (dialogue != null) dialogue.SayPurchaseFail();
    }
}
