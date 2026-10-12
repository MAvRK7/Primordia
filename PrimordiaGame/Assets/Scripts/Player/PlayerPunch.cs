using System;
using Primordia;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>Empty-hand punching and temporary trigger-held corpse butchering.</summary>
public sealed class PlayerPunch : MonoBehaviour
{
    const float InputPressThreshold = 0.7f;
    const float InputReleaseThreshold = 0.2f;
    const float ButcherRange = 3f;
    const float ButcherHoldDelay = 0.15f;

    [SerializeField] InputActionReference grip;
    [SerializeField] InputActionReference trigger;
    [SerializeField] InputActionReference isTracked;
    [SerializeField, Min(0f)] float damage = 12f;
    [SerializeField, Min(0.01f)] float radius = 0.12f;
    [SerializeField, Min(0f)] float minimumSpeed = 1.2f;
    [SerializeField] Transform butcherKnifeModel;
    [SerializeField] Transform aimOrigin;
    [SerializeField] Transform palmGripOrigin;
    [SerializeField, Min(0f)] float butcherMinimumSpeed = 0.35f;

    XROrigin origin;
    PlayerHealth player;
    XRBaseInteractor[] interactors;
    readonly Vector3[] previousPositions = new Vector3[9];
    bool hasPreviousPosition;
    bool ready = true;
    float nextHitTime;
    Transform knife;
    TMP_Text butcherPrompt;
    bool knifeTriggerArmed;
    bool knifeTriggerHeld;
    DinoCorpse knifeRequestTarget;
    float knifeEquipAt;
    DinoCorpse knifeTarget;
    Collider knifeTargetCollider;
    DinoCorpse knifeContact;
    Vector3 knifeBladeBase;
    Vector3 knifeBladeTip;

    bool HasKnife => knife != null && knife.gameObject.activeSelf;
    public bool IsHoldingButcherKnife => HasKnife;
    public Transform AimOrigin => aimOrigin;
    public Transform PalmGripOrigin => palmGripOrigin;
    public WeaponPalmGrip ButcherGrip => butcherKnifeModel != null
        ? butcherKnifeModel.GetComponentInParent<WeaponPalmGrip>() : null;
    public static Vector3 KnifeHandlePosition => new Vector3(0f, 0.018f, 0.00521f);

    void Awake()
    {
        origin = GetComponentInParent<XROrigin>();
        player = GetComponentInParent<PlayerHealth>();
        interactors = GetComponentsInChildren<XRBaseInteractor>(true);
        if (aimOrigin == null && transform.parent != null)
            aimOrigin = transform.parent.Find(transform.name + " Aim Pose");
        if (palmGripOrigin == null && transform.parent != null)
            palmGripOrigin = transform.parent.Find(transform.name + " Item Pose/Palm Grip Anchor");
    }

    void OnEnable()
    {
        hasPreviousPosition = false;
        knifeTriggerArmed = false;
        knifeTriggerHeld = false;
    }

    void OnDisable() => ResetHand();

    void ResetHand()
    {
        HideKnife();
        knifeTriggerArmed = false;
        if (butcherPrompt != null) butcherPrompt.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        // Consume input even while tracking is lost or this hand holds an item.
        // Releasing an item or killing a dinosaur must not turn a held trigger
        // into a new request for the temporary knife.
        bool triggerPressed = ReadKnifeTrigger(out bool triggerHeld);
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
        UpdateButcherKnife(triggerHeld, triggerPressed);
        if (HasKnife)
        {
            // The handle sits across the curled fingers; the blade extends up,
            // perpendicular to the pointing direction, rather than through the palm.
            knife.rotation = palmGripOrigin != null ? palmGripOrigin.rotation :
                aimOrigin != null ? aimOrigin.rotation : transform.rotation;
            var gripPosition = palmGripOrigin != null ? palmGripOrigin.position : transform.position;
            knife.position = gripPosition - knife.rotation * KnifeHandlePosition;
        }

        // Measure relative to the rig so walking, turning and respawning cannot punch.
        var hitRadius = HasKnife ? 0.015f : radius;
        int sampleCount = HasKnife ? previousPositions.Length : 1;
        if (!hasPreviousPosition)
        {
            for (int i = 0; i < sampleCount; i++)
                previousPositions[i] = origin.transform.InverseTransformPoint(HitPosition(i));
            hasPreviousPosition = true;
            return;
        }
        // A blade must leave its last contact before another strike can count.
        if (HasKnife && knifeContact != null)
        {
            bool touching = false;
            foreach (var overlap in Physics.OverlapCapsule(knife.TransformPoint(knifeBladeBase),
                knife.TransformPoint(knifeBladeTip), hitRadius + 0.01f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (overlap.GetComponentInParent<DinoCorpse>() == knifeContact) { touching = true; break; }
            if (!touching)
                knifeContact = null;
            for (int i = 0; i < sampleCount; i++)
                previousPositions[i] = origin.transform.InverseTransformPoint(HitPosition(i));
            return; // Withdrawal is not a second impact.
        }
        var closed = HasKnife || (Read(grip) > InputPressThreshold && Read(trigger) > InputPressThreshold);
        bool struck = false;
        // Sweep the visible blade along its length, rather than an arbitrary tip point.
        for (int i = 0; i < sampleCount; i++)
        {
            var hitPosition = HitPosition(i);
            var start = origin.transform.TransformPoint(previousPositions[i]);
            var delta = hitPosition - start;
            previousPositions[i] = origin.transform.InverseTransformPoint(hitPosition);
            var distance = delta.magnitude;
            var speed = distance / Mathf.Max(Time.deltaTime, 0.001f);
            if (HasKnife) ready = true;
            else if (!closed || speed < 0.5f) ready = true;
            if (struck || !closed || !ready || speed < (HasKnife ? butcherMinimumSpeed : minimumSpeed) ||
                distance > 0.75f || distance < 0.0001f || Time.time < nextHitTime) continue;

            // Sphere casts omit colliders already overlapping the starting sphere.
            bool blocked = false;
            foreach (var collider in Physics.OverlapSphere(start, hitRadius,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (IgnoreCollider(collider)) continue;
                struck = TryHit(collider, start, -delta);
                blocked = true;
                break;
            }
            // A solid obstruction blocks this part of the blade, but other
            // parts may still hit the corpse (for example beside the ground).
            if (blocked) continue;
            var hits = Physics.SphereCastAll(start, hitRadius, delta / distance, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (IgnoreCollider(hit.collider)) continue;
                struck = TryHit(hit.collider, hit.point, hit.normal);
                break;
            }
        }
    }

    Vector3 HitPosition(int sample) => HasKnife
        ? knife.TransformPoint(Vector3.Lerp(knifeBladeBase, knifeBladeTip,
            (float)sample / (previousPositions.Length - 1))) : transform.position;

    void FindBladeEndpoints()
    {
        foreach (var filter in knife.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null || filter.name.IndexOf("blade", StringComparison.OrdinalIgnoreCase) < 0) continue;
            var meshBounds = filter.sharedMesh.bounds;
            var bladeBounds = new Bounds();
            bool first = true;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                var point = knife.InverseTransformPoint(filter.transform.TransformPoint(
                    meshBounds.center + Vector3.Scale(meshBounds.extents, new Vector3(x, y, z))));
                if (first) { bladeBounds = new Bounds(point, Vector3.zero); first = false; }
                else bladeBounds.Encapsulate(point);
            }
            knifeBladeBase = new Vector3(bladeBounds.center.x, bladeBounds.min.y, bladeBounds.center.z);
            knifeBladeTip = new Vector3(bladeBounds.center.x, bladeBounds.max.y, bladeBounds.center.z);
            return;
        }
        Debug.LogError("Butcher knife model needs its blade mesh.", this);
    }

    bool TryHit(Collider collider, Vector3 point, Vector3 normal)
    {
        if (HasKnife)
        {
            var corpse = collider.GetComponentInParent<DinoCorpse>();
            if (corpse == null || !corpse.CanButcher) return false;
            knifeContact = corpse;
            ready = false;
            nextHitTime = Time.time + 0.25f;
            if (!corpse.HitWithKnife(point, normal)) return false;
            PulseHand(corpse.IsButchered ? 0.7f : 0.45f, corpse.IsButchered ? 0.14f : 0.07f);
            return true;
        }
        var dino = collider.GetComponentInParent<DinoHealth>();
        if (dino == null || dino.IsDead || damage <= 0f) return false;
        ready = false;
        nextHitTime = Time.time + 0.4f;
        dino.ApplyHit(new CombatHit(damage, point, normal, player.transform,
            impactKind: CombatImpactKind.Punch));
        PulseHand(0.4f, 0.07f);
        return true;
    }

    void PulseHand(float amplitude, float duration)
    {
        foreach (var interactor in interactors)
            if (interactor is XRBaseInputInteractor input && input.SendHapticImpulse(amplitude, duration))
                break;
    }

    bool IgnoreCollider(Collider collider) => collider.transform.IsChildOf(origin.transform) ||
        (collider.isTrigger && collider.GetComponentInParent<DinoCorpse>() == null);

    bool ReadKnifeTrigger(out bool held)
    {
        var action = trigger != null ? trigger.action : null;
        if (action == null || !action.enabled)
        {
            knifeTriggerArmed = false;
            knifeTriggerHeld = false;
            held = false;
            return false;
        }

        var value = Read(trigger);
        knifeTriggerHeld = value > (knifeTriggerHeld ? InputReleaseThreshold : InputPressThreshold);
        held = knifeTriggerHeld;
        if (value <= InputReleaseThreshold)
        {
            knifeTriggerArmed = true;
            return false;
        }
        if (!held || !knifeTriggerArmed) return false;
        knifeTriggerArmed = false;
        return true;
    }

    void UpdateButcherKnife(bool held, bool pressed)
    {
        if (!held || (HasKnife && (knifeTarget == null || !knifeTarget.CanButcher ||
            !knifeTarget.gameObject.activeInHierarchy || knifeTargetCollider == null ||
            !knifeTargetCollider.enabled || !knifeTargetCollider.gameObject.activeInHierarchy ||
            (knifeTargetCollider.ClosestPoint(transform.position) - transform.position).sqrMagnitude >
            ButcherRange * ButcherRange))) HideKnife();

        DinoCorpse eligibleCorpse = null;
        Collider eligibleCollider = null;
        // Wait for an open hand so pressing the trigger just before squeezing
        // grip to punch cannot deploy a knife during that gesture.
        var gripReleased = Read(grip) <= InputReleaseThreshold;
        if (!HasKnife && butcherKnifeModel != null)
        {
            var aiming = aimOrigin != null ? aimOrigin : transform;
            var hits = Physics.RaycastAll(aiming.position, aiming.forward, ButcherRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (IgnoreCollider(hit.collider)) continue;
                var corpse = hit.collider.GetComponentInParent<DinoCorpse>();
                if (corpse != null && corpse.CanButcher)
                {
                    eligibleCorpse = corpse;
                    eligibleCollider = hit.collider;
                }
                break;
            }
        }

        if (!held || !gripReleased || eligibleCorpse != knifeRequestTarget)
            ClearKnifeRequest();
        if (eligibleCorpse != null && pressed && gripReleased)
        {
            knifeRequestTarget = eligibleCorpse;
            knifeEquipAt = Time.time + ButcherHoldDelay;
        }
        if (knifeRequestTarget != null && Time.time >= knifeEquipAt)
        {
            if (knife == null)
            {
                // Reuse just the knife's model, without its grab or combat components.
                knife = Instantiate(butcherKnifeModel, transform, false);
                knife.name = "Butcher Knife";
                knife.localRotation = Quaternion.identity;
                knife.localPosition = -(knife.localRotation * KnifeHandlePosition);
                foreach (var collider in knife.GetComponentsInChildren<Collider>())
                    collider.enabled = false;
                FindBladeEndpoints();
            }
            knife.gameObject.SetActive(true);
            knifeTarget = eligibleCorpse;
            knifeTargetCollider = eligibleCollider;
            ClearKnifeRequest();
            hasPreviousPosition = false;
            knifeContact = null;
            ready = true;
        }

        var showPrompt = eligibleCorpse != null && !HasKnife && gripReleased;
        if (showPrompt && butcherPrompt == null)
        {
            butcherPrompt = new GameObject("Butcher Prompt").AddComponent<TextMeshPro>();
            butcherPrompt.transform.SetParent(transform, false);
            butcherPrompt.transform.localPosition = new Vector3(0f, 0.12f, 0.15f);
            butcherPrompt.rectTransform.sizeDelta = new Vector2(0.45f, 0.12f);
            butcherPrompt.fontSize = 0.55f;
            butcherPrompt.alignment = TextAlignmentOptions.Center;
            butcherPrompt.textWrappingMode = TextWrappingModes.NoWrap;
            butcherPrompt.overflowMode = TextOverflowModes.Overflow;
            butcherPrompt.text = "[Trigger] Butcher";
        }
        if (butcherPrompt != null)
        {
            butcherPrompt.gameObject.SetActive(showPrompt);
            if (origin.Camera != null)
                butcherPrompt.transform.rotation = Quaternion.LookRotation(
                    butcherPrompt.transform.position - origin.Camera.transform.position);
        }
    }

    void HideKnife()
    {
        ClearKnifeRequest();
        knifeTarget = null;
        knifeTargetCollider = null;
        if (!HasKnife) return;
        knife.gameObject.SetActive(false);
        hasPreviousPosition = false;
        knifeContact = null;
        ready = true;
    }

    void ClearKnifeRequest()
    {
        knifeRequestTarget = null;
        knifeEquipAt = 0f;
    }

    static float Read(InputActionReference reference) =>
        reference != null && reference.action.enabled ? reference.action.ReadValue<float>() : 0f;
}
