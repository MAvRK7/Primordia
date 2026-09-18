using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>Collecting a drop deposits its resources directly into the campsite shop sack.</summary>
[RequireComponent(typeof(XRGrabInteractable))]
public sealed class DinoLootPickup : MonoBehaviour
{
    public int meat;
    public int bones;
    bool collected;

    void OnEnable() => GetComponent<XRGrabInteractable>().selectEntered.AddListener(Collect);
    void OnDisable() => GetComponent<XRGrabInteractable>().selectEntered.RemoveListener(Collect);

    void Collect(SelectEnterEventArgs args)
    {
        if (collected || args.interactorObject.transform.GetComponentInParent<PlayerHealth>() == null)
            return;

        foreach (var shop in FindObjectsByType<ShopManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (shop.gameObject.scene != gameObject.scene || shop.resourceSack == null)
                continue;

            collected = true;
            shop.resourceSack.AddResources(meat, bones);
            Destroy(gameObject);
            return;
        }
    }
}
