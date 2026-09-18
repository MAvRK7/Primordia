using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>A closed, empty controller hand damages one dinosaur per physical swing.</summary>
public sealed class PlayerPunch : MonoBehaviour
{
    [SerializeField] InputActionReference grip;
    [SerializeField] InputActionReference trigger;
    [SerializeField] InputActionReference isTracked;
    [SerializeField, Min(0f)] float damage = 8f;
    [SerializeField, Min(0.01f)] float radius = 0.12f;
    [SerializeField, Min(0f)] float minimumSpeed = 1.2f;

    XROrigin origin;
    PlayerHealth player;
    XRBaseInteractor[] interactors;
    Vector3 previousPosition;
    bool hasPreviousPosition;
    bool ready = true;
    float nextHitTime;

    void Awake()
    {
        origin = GetComponentInParent<XROrigin>();
        player = GetComponentInParent<PlayerHealth>();
        interactors = GetComponentsInChildren<XRBaseInteractor>(true);
    }

    void OnEnable() => hasPreviousPosition = false;

    void LateUpdate()
    {
        if (origin == null || player == null || player.IsDead || Read(isTracked) < 0.5f)
        {
            hasPreviousPosition = false;
            return;
        }

        // Measure relative to the rig so walking, turning and respawning cannot punch.
        var position = origin.transform.InverseTransformPoint(transform.position);
        var start = origin.transform.TransformPoint(previousPosition);
        var delta = transform.position - start;
        previousPosition = position;
        if (!hasPreviousPosition)
        {
            hasPreviousPosition = true;
            return;
        }

        var distance = delta.magnitude;
        var speed = distance / Mathf.Max(Time.deltaTime, 0.001f);
        var closed = Read(grip) > 0.7f && Read(trigger) > 0.7f;
        if (!closed || speed < 0.5f) ready = true;
        if (!closed || !ready || speed < minimumSpeed || distance > 0.75f || Time.time < nextHitTime)
            return;
        foreach (var interactor in interactors)
            if (interactor.hasSelection) return;

        // Sphere casts omit colliders already overlapping the starting sphere.
        foreach (var collider in Physics.OverlapSphere(start, radius,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (collider.transform.IsChildOf(origin.transform)) continue;
            TryHit(collider, collider.ClosestPoint(start), -delta);
            return;
        }

        // Sweep the fist to catch fast swings and stop at the first solid obstruction.
        var hits = Physics.SphereCastAll(start, radius, delta / distance, distance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.transform.IsChildOf(origin.transform)) continue;
            TryHit(hit.collider, hit.point, -delta);
            return;
        }
    }

    void TryHit(Collider collider, Vector3 point, Vector3 normal)
    {
        var dino = collider.GetComponentInParent<DinoHealth>();
        if (dino == null || dino.IsDead) return;
        ready = false;
        nextHitTime = Time.time + 0.4f;
        dino.ApplyHit(new CombatHit(damage, point, normal, player.transform));
    }

    static float Read(InputActionReference reference) =>
        reference != null && reference.action.enabled ? reference.action.ReadValue<float>() : 0f;
}
