using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// A physically swung melee weapon. Blade contacts only deal damage while the
/// weapon is held by an input interactor and moving above the impact threshold.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XRGrabInteractable))]
public sealed class Melee : Item
{
    [Header("Definition")]
    [SerializeField] WeaponDefinition m_Definition;

    [Header("Interaction")]
    [SerializeField] XRGrabInteractable m_GrabInteractable;
    [SerializeField] MeleeHitbox m_Hitbox;
    [SerializeField, Min(0f)] float m_MinimumHitSpeed = 1f;

    [Header("Feedback")]
    [SerializeField] AudioSource m_AudioSource;
    [SerializeField] AudioClip m_HitClip;
    [SerializeField, Range(0f, 1f)] float m_HitHapticAmplitude = 0.25f;
    [SerializeField, Min(0f)] float m_HitHapticDuration = 0.04f;

    readonly Dictionary<IDamageable, float> m_NextHitTimes = new Dictionary<IDamageable, float>();

    XRBaseInputInteractor m_ActiveInteractor;
    Transform m_HolderRoot;
    Vector3 m_PreviousBladePosition;
    Vector3 m_BladeVelocity;
    bool m_HasBladeSample;

    public WeaponDefinition Definition => m_Definition;

    void Awake()
    {
        if (m_GrabInteractable == null)
            m_GrabInteractable = GetComponent<XRGrabInteractable>();

        if (m_AudioSource == null)
            m_AudioSource = GetComponent<AudioSource>();

        SetHitboxActive(false);
    }

    void OnEnable()
    {
        if (m_GrabInteractable == null)
            return;

        m_GrabInteractable.selectEntered.AddListener(HandleSelectEntered);
        m_GrabInteractable.selectExited.AddListener(HandleSelectExited);
    }

    void OnDisable()
    {
        if (m_GrabInteractable != null)
        {
            m_GrabInteractable.selectEntered.RemoveListener(HandleSelectEntered);
            m_GrabInteractable.selectExited.RemoveListener(HandleSelectExited);
        }

        Disarm();
    }

    void FixedUpdate()
    {
        if (!IsHeldByActiveInteractor())
        {
            m_BladeVelocity = Vector3.zero;
            m_HasBladeSample = false;
            return;
        }

        var bladePosition = GetBladeSamplePosition();
        if (m_HasBladeSample && Time.fixedDeltaTime > Mathf.Epsilon)
            m_BladeVelocity = (bladePosition - m_PreviousBladePosition) / Time.fixedDeltaTime;
        else
            m_BladeVelocity = Vector3.zero;

        m_PreviousBladePosition = bladePosition;
        m_HasBladeSample = true;
    }

    void HandleSelectEntered(SelectEnterEventArgs args)
    {
        if (args.interactorObject is not XRBaseInputInteractor inputInteractor)
        {
            Disarm();
            return;
        }

        m_ActiveInteractor = inputInteractor;
        m_HolderRoot = inputInteractor.transform.root;
        m_NextHitTimes.Clear();
        SetHitboxActive(true);
        ResetBladeSample();
    }

    void HandleSelectExited(SelectExitEventArgs args)
    {
        if (ReferenceEquals(args.interactorObject, m_ActiveInteractor))
            Disarm();
    }

    internal void RegisterHit(Collider other, Collider sourceCollider)
    {
        if (!IsHeldByActiveInteractor() ||
            m_Definition == null ||
            m_Definition.Type != WeaponType.Melee ||
            other == null ||
            other.transform.IsChildOf(transform) ||
            m_BladeVelocity.magnitude < m_MinimumHitSpeed)
        {
            return;
        }

        if (m_HolderRoot != null &&
            (other.transform == m_HolderRoot || other.transform.IsChildOf(m_HolderRoot)))
        {
            return;
        }

        if (!CombatDamageResolver.TryGetDamageable(other, out var target))
            return;

        if (m_NextHitTimes.TryGetValue(target, out var nextHitTime) && Time.time < nextHitTime)
            return;

        m_NextHitTimes[target] = Time.time + 1f / m_Definition.AttacksPerSecond;

        var origin = sourceCollider != null
            ? sourceCollider.bounds.center
            : transform.position;
        var point = other.ClosestPoint(origin);
        var normal = point - origin;
        if (normal.sqrMagnitude <= Mathf.Epsilon)
            normal = -m_BladeVelocity;

        target.ApplyHit(new CombatHit(
            m_Definition.Damage,
            point,
            normal,
            m_ActiveInteractor.transform));

        PlayOneShot(m_HitClip);
        PlayerNoise.EmitNoise(point, m_Definition.NoiseRange);
        m_ActiveInteractor.SendHapticImpulse(m_HitHapticAmplitude, m_HitHapticDuration);
    }

    bool IsHeldByActiveInteractor()
    {
        return m_ActiveInteractor != null &&
            m_GrabInteractable != null &&
            m_GrabInteractable.interactorsSelecting.Contains(m_ActiveInteractor);
    }

    Vector3 GetBladeSamplePosition()
    {
        var hitCollider = m_Hitbox != null ? m_Hitbox.HitCollider : null;
        return hitCollider != null
            ? hitCollider.bounds.center
            : transform.position;
    }

    void ResetBladeSample()
    {
        m_PreviousBladePosition = GetBladeSamplePosition();
        m_BladeVelocity = Vector3.zero;
        m_HasBladeSample = true;
    }

    void Disarm()
    {
        m_ActiveInteractor = null;
        m_HolderRoot = null;
        m_BladeVelocity = Vector3.zero;
        m_HasBladeSample = false;
        m_NextHitTimes.Clear();
        SetHitboxActive(false);
    }

    void SetHitboxActive(bool active)
    {
        if (m_Hitbox != null)
            m_Hitbox.SetActive(active);
    }

    void PlayOneShot(AudioClip clip)
    {
        if (m_AudioSource != null && clip != null)
            m_AudioSource.PlayOneShot(clip);
    }

    void OnValidate()
    {
        m_MinimumHitSpeed = Mathf.Max(0f, m_MinimumHitSpeed);
        m_HitHapticDuration = Mathf.Max(0f, m_HitHapticDuration);
    }
}
