using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// A held hitscan gun. XRI Activate fires; the configured Reload action reloads.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XRGrabInteractable))]
public sealed class Ranged : Item
{
    [Serializable]
    public sealed class AmmoChangedEvent : UnityEvent<int, int>
    {
    }

    [Header("Definition")]
    [SerializeField] WeaponDefinition m_Definition;
    [SerializeField] Ammo m_ReserveAmmo;

    [Header("Interaction")]
    [SerializeField] XRGrabInteractable m_GrabInteractable;
    [SerializeField] InputActionReference m_ReloadAction;

    [Header("Raycast")]
    [SerializeField] Transform m_FirePoint;
    [SerializeField] LayerMask m_HitMask = ~0;

    [Header("Feedback")]
    [SerializeField] Transform m_VisualRoot;
    [SerializeField] ParticleSystem m_MuzzleFlash;
    [SerializeField] ParticleSystem m_ImpactEffect;
    [SerializeField] LineRenderer m_Tracer;
    [SerializeField, Min(0.01f)] float m_TracerDuration = 0.05f;
    [SerializeField] AudioSource m_AudioSource;
    [SerializeField] AudioClip m_ShotClip;
    [SerializeField] AudioClip m_DryFireClip;
    [SerializeField] AudioClip m_ReloadClip;
    [SerializeField, Range(0f, 1f)] float m_ShotHapticAmplitude = 0.45f;
    [SerializeField, Min(0f)] float m_ShotHapticDuration = 0.06f;
    [SerializeField, Min(0f)] float m_RecoilDistance = 0.018f;
    [SerializeField, Min(0f)] float m_RecoilAngle = 8f;
    [SerializeField, Min(0f)] float m_ReloadDuration = 0.8f;

    [Header("Events")]
    [SerializeField] AmmoChangedEvent m_AmmoChanged = new AmmoChangedEvent();

    readonly RaycastHit[] m_HitBuffer = new RaycastHit[16];

    int m_CurrentMagazine;
    float m_NextFireTime;
    bool m_TriggerHeld;
    bool m_IsReloading;
    bool m_ReloadActionWasEnabled;
    XRBaseInputInteractor m_ActiveInteractor;
    Coroutine m_TracerRoutine;
    Coroutine m_RecoilRoutine;
    Coroutine m_ReloadRoutine;
    Vector3 m_VisualStartPosition;
    Quaternion m_VisualStartRotation;

    public WeaponDefinition Definition => m_Definition;
    public int CurrentMagazine => m_CurrentMagazine;
    public int ReserveRounds => m_ReserveAmmo != null ? m_ReserveAmmo.Rounds : 0;
    public bool IsReloading => m_IsReloading;

    void Awake()
    {
        if (m_GrabInteractable == null)
            m_GrabInteractable = GetComponent<XRGrabInteractable>();

        if (m_FirePoint == null)
            m_FirePoint = transform;

        if (m_AudioSource == null)
            m_AudioSource = GetComponent<AudioSource>();

        if (m_VisualRoot != null)
        {
            m_VisualStartPosition = m_VisualRoot.localPosition;
            m_VisualStartRotation = m_VisualRoot.localRotation;
        }

        m_CurrentMagazine = m_Definition != null
            ? m_Definition.MagazineCapacity
            : 0;

        SetTracerVisible(false);
        NotifyAmmoChanged();
    }

    void OnEnable()
    {
        if (m_GrabInteractable != null)
        {
            m_GrabInteractable.activated.AddListener(HandleActivated);
            m_GrabInteractable.deactivated.AddListener(HandleDeactivated);
        }

        var action = m_ReloadAction != null ? m_ReloadAction.action : null;
        if (action != null)
        {
            action.performed += HandleReloadPerformed;
            m_ReloadActionWasEnabled = action.enabled;
            if (!m_ReloadActionWasEnabled)
                action.Enable();
        }
    }

    void OnDisable()
    {
        if (m_GrabInteractable != null)
        {
            m_GrabInteractable.activated.RemoveListener(HandleActivated);
            m_GrabInteractable.deactivated.RemoveListener(HandleDeactivated);
        }

        var action = m_ReloadAction != null ? m_ReloadAction.action : null;
        if (action != null)
        {
            action.performed -= HandleReloadPerformed;
            if (!m_ReloadActionWasEnabled)
                action.Disable();
        }

        m_TriggerHeld = false;
        m_ActiveInteractor = null;
        m_IsReloading = false;
        StopAllCoroutines();
        m_TracerRoutine = null;
        m_RecoilRoutine = null;
        m_ReloadRoutine = null;
        SetTracerVisible(false);

        if (m_VisualRoot != null)
            m_VisualRoot.SetLocalPositionAndRotation(m_VisualStartPosition, m_VisualStartRotation);
    }

    void Update()
    {
        if (m_TriggerHeld && m_Definition != null && m_Definition.Automatic)
            TryFire(m_ActiveInteractor != null ? m_ActiveInteractor.transform : transform);
    }

    void HandleActivated(ActivateEventArgs args)
    {
        m_TriggerHeld = true;
        m_ActiveInteractor = args.interactorObject as XRBaseInputInteractor;
        TryFire(args.interactorObject.transform);
    }

    void HandleDeactivated(DeactivateEventArgs args)
    {
        m_TriggerHeld = false;
        m_ActiveInteractor = null;
    }

    void HandleReloadPerformed(InputAction.CallbackContext context)
    {
        if (m_GrabInteractable == null || m_GrabInteractable.isSelected)
            Reload();
    }

    public bool TryFire(Transform attacker = null)
    {
        if (m_Definition == null || m_Definition.Type != WeaponType.Gun)
            return false;

        if (m_IsReloading || Time.time < m_NextFireTime)
            return false;

        if (m_CurrentMagazine <= 0)
        {
            m_NextFireTime = Time.time + 0.2f;
            PlayOneShot(m_DryFireClip);
            return false;
        }

        m_CurrentMagazine--;
        m_NextFireTime = Time.time + 1f / m_Definition.AttacksPerSecond;
        NotifyAmmoChanged();

        var ray = new Ray(m_FirePoint.position, m_FirePoint.forward);
        var hasHit = TryFindClosestHit(ray, out var hit);
        var traceEnd = hasHit
            ? hit.point
            : ray.GetPoint(m_Definition.Range);

        if (hasHit)
        {
            var combatHit = new CombatHit(
                m_Definition.Damage,
                hit.point,
                hit.normal,
                attacker != null ? attacker : transform);

            CombatDamageResolver.TryApply(hit.collider, combatHit, out _);
            PlayImpact(hit.point, hit.normal);
        }

        PlayShotFeedback(traceEnd);
        PlayerNoise.EmitNoise(m_FirePoint.position, m_Definition.NoiseRange);
        return true;
    }

    public bool Reload()
    {
        if (m_Definition == null ||
            m_Definition.Type != WeaponType.Gun ||
            m_IsReloading ||
            m_CurrentMagazine >= m_Definition.MagazineCapacity ||
            m_ReserveAmmo == null ||
            m_ReserveAmmo.Type != m_Definition.AmmoType ||
            m_ReserveAmmo.Rounds <= 0)
        {
            return false;
        }

        m_ReloadRoutine = StartCoroutine(ReloadRoutine());
        return true;
    }

    IEnumerator ReloadRoutine()
    {
        m_IsReloading = true;
        m_TriggerHeld = false;
        PlayOneShot(m_ReloadClip);

        if (m_ReloadDuration > 0f)
            yield return new WaitForSeconds(m_ReloadDuration);

        var needed = m_Definition.MagazineCapacity - m_CurrentMagazine;
        m_CurrentMagazine += m_ReserveAmmo.Take(m_Definition.AmmoType, needed);
        m_IsReloading = false;
        m_ReloadRoutine = null;
        NotifyAmmoChanged();
    }

    bool TryFindClosestHit(Ray ray, out RaycastHit closestHit)
    {
        var hitCount = Physics.RaycastNonAlloc(
            ray,
            m_HitBuffer,
            m_Definition.Range,
            m_HitMask,
            QueryTriggerInteraction.Ignore);

        closestHit = default;
        var closestDistance = float.PositiveInfinity;
        var found = false;

        for (var index = 0; index < hitCount; index++)
        {
            var candidate = m_HitBuffer[index];
            if (candidate.collider == null || candidate.collider.transform.IsChildOf(transform))
                continue;

            if (candidate.distance >= closestDistance)
                continue;

            closestHit = candidate;
            closestDistance = candidate.distance;
            found = true;
        }

        return found;
    }

    void PlayShotFeedback(Vector3 traceEnd)
    {
        if (m_MuzzleFlash != null)
            m_MuzzleFlash.Play(true);

        if (m_Tracer != null)
        {
            m_Tracer.positionCount = 2;
            m_Tracer.SetPosition(0, m_FirePoint.position);
            m_Tracer.SetPosition(1, traceEnd);
            SetTracerVisible(true);

            if (m_TracerRoutine != null)
                StopCoroutine(m_TracerRoutine);
            m_TracerRoutine = StartCoroutine(HideTracerRoutine());
        }

        PlayOneShot(m_ShotClip);
        m_ActiveInteractor?.SendHapticImpulse(m_ShotHapticAmplitude, m_ShotHapticDuration);

        if (m_VisualRoot != null)
        {
            if (m_RecoilRoutine != null)
                StopCoroutine(m_RecoilRoutine);
            m_RecoilRoutine = StartCoroutine(RecoilRoutine());
        }
    }

    IEnumerator HideTracerRoutine()
    {
        yield return new WaitForSeconds(m_TracerDuration);
        SetTracerVisible(false);
        m_TracerRoutine = null;
    }

    IEnumerator RecoilRoutine()
    {
        const float kickDuration = 0.045f;
        const float returnDuration = 0.09f;
        var kickPosition = m_VisualStartPosition - Vector3.forward * m_RecoilDistance;
        var kickRotation = m_VisualStartRotation * Quaternion.Euler(-m_RecoilAngle, 0f, 0f);

        yield return AnimateVisual(m_VisualStartPosition, kickPosition, m_VisualStartRotation, kickRotation, kickDuration);
        yield return AnimateVisual(kickPosition, m_VisualStartPosition, kickRotation, m_VisualStartRotation, returnDuration);

        m_VisualRoot.SetLocalPositionAndRotation(m_VisualStartPosition, m_VisualStartRotation);
        m_RecoilRoutine = null;
    }

    IEnumerator AnimateVisual(
        Vector3 fromPosition,
        Vector3 toPosition,
        Quaternion fromRotation,
        Quaternion toRotation,
        float duration)
    {
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var amount = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            m_VisualRoot.SetLocalPositionAndRotation(
                Vector3.LerpUnclamped(fromPosition, toPosition, amount),
                Quaternion.SlerpUnclamped(fromRotation, toRotation, amount));
            yield return null;
        }
    }

    void PlayImpact(Vector3 point, Vector3 normal)
    {
        if (m_ImpactEffect == null)
            return;

        m_ImpactEffect.transform.SetPositionAndRotation(point, Quaternion.LookRotation(normal));
        m_ImpactEffect.Play(true);
    }

    void PlayOneShot(AudioClip clip)
    {
        if (m_AudioSource != null && clip != null)
            m_AudioSource.PlayOneShot(clip);
    }

    void SetTracerVisible(bool visible)
    {
        if (m_Tracer != null)
            m_Tracer.enabled = visible;
    }

    void NotifyAmmoChanged()
    {
        m_AmmoChanged?.Invoke(m_CurrentMagazine, ReserveRounds);
    }

    [Obsolete("Use TryFire instead.")]
    public bool fire()
    {
        return TryFire(transform);
    }

    void OnValidate()
    {
        m_TracerDuration = Mathf.Max(0.01f, m_TracerDuration);
        m_ShotHapticDuration = Mathf.Max(0f, m_ShotHapticDuration);
        m_RecoilDistance = Mathf.Max(0f, m_RecoilDistance);
        m_RecoilAngle = Mathf.Max(0f, m_RecoilAngle);
        m_ReloadDuration = Mathf.Max(0f, m_ReloadDuration);
    }
}
