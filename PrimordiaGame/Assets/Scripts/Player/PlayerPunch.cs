using System;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>Empty-hand punching and temporary trigger-held corpse butchering.</summary>
public sealed class PlayerPunch : MonoBehaviour
{
    [SerializeField] InputActionReference grip;
    [SerializeField] InputActionReference trigger;
    [SerializeField] InputActionReference isTracked;
    [SerializeField, Min(0f)] float damage = 8f;
    [SerializeField, Min(0.01f)] float radius = 0.12f;
    [SerializeField, Min(0f)] float minimumSpeed = 1.2f;
    [SerializeField] Transform butcherKnifeModel;

    XROrigin origin;
    PlayerHealth player;
    XRBaseInteractor[] interactors;
    Vector3 previousPosition;
    bool hasPreviousPosition;
    bool ready = true;
    float nextHitTime;
    Transform knife;
    TMP_Text butcherPrompt;
    Collider knifeContact;

    bool HasKnife => knife != null && knife.gameObject.activeSelf;

    void Awake()
    {
        origin = GetComponentInParent<XROrigin>();
        player = GetComponentInParent<PlayerHealth>();
        interactors = GetComponentsInChildren<XRBaseInteractor>(true);
    }

    void OnEnable() => hasPreviousPosition = false;

    void OnDisable() => ResetHand();

    void ResetHand()
    {
        HideKnife();
        if (butcherPrompt != null) butcherPrompt.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (origin == null || player == null || player.IsDead || Read(isTracked) < 0.5f)
        {
            ResetHand();
            hasPreviousPosition = false;
            return;
        }

        foreach (var interactor in interactors)
        {
            if (!interactor.hasSelection) continue;
            ResetHand();
            hasPreviousPosition = false;
            return;
        }
        UpdateButcherKnife();

        // Measure relative to the rig so walking, turning and respawning cannot punch.
        var hitPosition = HasKnife ? knife.TransformPoint(new Vector3(0f, 0.2f, 0f)) : transform.position;
        var hitRadius = HasKnife ? 0.05f : radius;
        var position = origin.transform.InverseTransformPoint(hitPosition);
        var start = origin.transform.TransformPoint(previousPosition);
        var delta = hitPosition - start;
        previousPosition = position;
        if (!hasPreviousPosition)
        {
            hasPreviousPosition = true;
            return;
        }

        var distance = delta.magnitude;
        var speed = distance / Mathf.Max(Time.deltaTime, 0.001f);
        var closed = HasKnife || (Read(grip) > 0.7f && Read(trigger) > 0.7f);
        // A blade must leave its last contact before another strike can count.
        if (HasKnife && knifeContact != null)
        {
            if (Vector3.Distance(knifeContact.ClosestPoint(hitPosition), hitPosition) > hitRadius + 0.02f)
                knifeContact = null;
            return; // Withdrawal is not a second impact.
        }
        if (HasKnife) ready = true;
        else if (!closed || speed < 0.5f) ready = true;
        if (!closed || !ready || speed < minimumSpeed || distance > 0.75f || Time.time < nextHitTime)
            return;
        var triggerInteraction = HasKnife ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;

        // Sphere casts omit colliders already overlapping the starting sphere.
        foreach (var collider in Physics.OverlapSphere(start, hitRadius,
            Physics.DefaultRaycastLayers, triggerInteraction))
        {
            if (IgnoreCollider(collider)) continue;
            TryHit(collider, collider.ClosestPoint(start), -delta);
            return;
        }

        // Sweep the fist to catch fast swings and stop at the first solid obstruction.
        var hits = Physics.SphereCastAll(start, hitRadius, delta / distance, distance,
            Physics.DefaultRaycastLayers, triggerInteraction);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (IgnoreCollider(hit.collider)) continue;
            TryHit(hit.collider, hit.point, -delta);
            return;
        }
    }

    void TryHit(Collider collider, Vector3 point, Vector3 normal)
    {
        if (HasKnife)
        {
            var corpse = collider.GetComponentInParent<DinoCorpse>();
            if (corpse == null || corpse.IsButchered) return;
            knifeContact = collider;
            ready = false;
            nextHitTime = Time.time + 0.4f;
            corpse.HitWithKnife();
            return;
        }
        var dino = collider.GetComponentInParent<DinoHealth>();
        if (dino == null || dino.IsDead) return;
        ready = false;
        nextHitTime = Time.time + 0.4f;
        dino.ApplyHit(new CombatHit(damage, point, normal, player.transform));
    }

    bool IgnoreCollider(Collider collider) => collider.transform.IsChildOf(origin.transform) ||
        (collider.isTrigger && collider.GetComponentInParent<DinoCorpse>() == null);

    void UpdateButcherKnife()
    {
        var held = Read(trigger) > 0.7f;
        if (!held) HideKnife();
        var eligible = false;
        if (!HasKnife && butcherKnifeModel != null)
        {
            var hits = Physics.RaycastAll(transform.position, transform.forward, 3f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (IgnoreCollider(hit.collider)) continue;
                var corpse = hit.collider.GetComponentInParent<DinoCorpse>();
                eligible = corpse != null && !corpse.IsButchered;
                break;
            }
        }

        if (eligible && held)
        {
            if (knife == null)
            {
                // Reuse just the knife's model, without its grab or combat components.
                knife = Instantiate(butcherKnifeModel, transform, false);
                knife.name = "Butcher Knife";
                knife.localRotation = Quaternion.Euler(90f, 0f, 0f);
                knife.localPosition = -(knife.localRotation * new Vector3(0f, 0.018f, 0f));
                foreach (var collider in knife.GetComponentsInChildren<Collider>())
                    collider.enabled = false;
            }
            knife.gameObject.SetActive(true);
            hasPreviousPosition = false;
            knifeContact = null;
            ready = true;
        }

        if (eligible && !held && butcherPrompt == null)
        {
            butcherPrompt = new GameObject("Butcher Prompt").AddComponent<TextMeshPro>();
            butcherPrompt.transform.SetParent(transform, false);
            butcherPrompt.transform.localPosition = new Vector3(0f, 0.12f, 0.15f);
            butcherPrompt.rectTransform.sizeDelta = new Vector2(0.45f, 0.12f);
            butcherPrompt.fontSize = 2.4f;
            butcherPrompt.alignment = TextAlignmentOptions.Center;
            butcherPrompt.text = "Hold trigger\nButcher knife";
        }
        if (butcherPrompt != null)
        {
            butcherPrompt.gameObject.SetActive(eligible && !held);
            if (origin.Camera != null)
                butcherPrompt.transform.rotation = Quaternion.LookRotation(
                    butcherPrompt.transform.position - origin.Camera.transform.position);
        }
    }

    void HideKnife()
    {
        if (!HasKnife) return;
        knife.gameObject.SetActive(false);
        hasPreviousPosition = false;
        knifeContact = null;
        ready = true;
    }

    static float Read(InputActionReference reference) =>
        reference != null && reference.action.enabled ? reference.action.ReadValue<float>() : 0f;
}
