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