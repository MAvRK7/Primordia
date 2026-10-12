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
    Vector3 m_FireStartPosition;
    Quaternion m_FireStartRotation = Quaternion.identity;
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
        if (m_FirePoint != null && m_FirePoint != transform)
        {
            m_FireStartPosition = m_FirePoint.localPosition;
            m_FireStartRotation = m_FirePoint.localRotation;
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
        if (m_FirePoint != null && m_FirePoint != transform)
            m_FirePoint.SetLocalPositionAndRotation(m_FireStartPosition, m_FireStartRotation);
    }

    void Update()
    {
        if (m_GrabInteractable == null || !m_GrabInteractable.isSelected)
        {
            m_TriggerHeld = false;
            m_ActiveInteractor = null;
        }
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
        var hasHit = TryFindClosestHit(ray, attacker, out var hit);
        var traceEnd = hasHit
            ? hit.point
            : ray.GetPoint(m_Definition.Range);
        bool enemyHit = false;

        if (hasHit)
        {
            var combatHit = new CombatHit(
                m_Definition.Damage,
                hit.point,
                hit.normal,
                attacker != null ? attacker : transform,
                impactKind: CombatImpactKind.Bullet);

            enemyHit = CombatDamageResolver.TryApply(hit.collider, combatHit, out var target) && target is DinoHealth;
            if (!enemyHit) PlayImpact(hit.point, hit.normal);
        }

        PlayShotFeedback(traceEnd);
        if (enemyHit && m_ActiveInteractor != null)
            StartCoroutine(ConfirmEnemyHit(m_ActiveInteractor));
        PlayerNoise.EmitNoise(m_FirePoint.position, m_Definition.NoiseRange);
        return true;
    }

    IEnumerator ConfirmEnemyHit(XRBaseInputInteractor interactor)
    {
        yield return new WaitForSeconds(0.08f);
        if (interactor != null && m_GrabInteractable.interactorsSelecting.Contains(interactor))
            interactor.SendHapticImpulse(0.25f, 0.035f);
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

    bool TryFindClosestHit(Ray ray, Transform attacker, out RaycastHit closestHit)
    {
        var hits = m_HitBuffer;
        var hitCount = Physics.RaycastNonAlloc(
            ray,
            hits,
            m_Definition.Range,
            m_HitMask,
            QueryTriggerInteraction.Ignore);

        // NonAlloc does not guarantee which hits it returns when the buffer is
        // full. A crowded herd must not hide the nearest dinosaur or obstruction.
        if (hitCount == hits.Length)
        {
            hits = Physics.RaycastAll(ray, m_Definition.Range, m_HitMask,
                QueryTriggerInteraction.Ignore);
            hitCount = hits.Length;
        }

        var holder = attacker != null ? attacker.GetComponentInParent<PlayerHealth>() : null;
        if (holder == null && m_ActiveInteractor != null)
            holder = m_ActiveInteractor.GetComponentInParent<PlayerHealth>();
        if (holder == null && m_GrabInteractable != null)
            foreach (var interactor in m_GrabInteractable.interactorsSelecting)
            {
                holder = interactor.transform.GetComponentInParent<PlayerHealth>();
                if (holder != null)
                    break;
            }
        var holderRoot = holder != null ? holder.transform : null;

        closestHit = default;
        var closestDistance = float.PositiveInfinity;
        var found = false;

        for (var index = 0; index < hitCount; index++)
        {
            var candidate = hits[index];
            if (IgnoreShotCollider(candidate.collider, holderRoot))
                continue;

            if (candidate.distance >= closestDistance)
                continue;

            closestHit = candidate;
            closestDistance = candidate.distance;
            found = true;
        }

        return found;
    }

    bool IgnoreShotCollider(Collider collider, Transform holderRoot)
    {
        if (collider == null || collider.transform.IsChildOf(transform))
            return true;
        if (holderRoot == null)
            return false;
        if (collider.transform.IsChildOf(holderRoot))
            return true;

        // Grabbed items keep their world parent. The other hand's weapon must
        // not catch a shot merely because it crosses the firing hand's muzzle.
        var heldItem = collider.GetComponentInParent<XRGrabInteractable>();
        if (heldItem != null)
            foreach (var interactor in heldItem.interactorsSelecting)
                if (interactor.transform.IsChildOf(holderRoot))
                    return true;
        return false;
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
        var gripPivot = m_GrabInteractable != null && m_GrabInteractable.attachTransform != null
            ? m_VisualRoot.parent.InverseTransformPoint(m_GrabInteractable.attachTransform.position)
            : m_VisualStartPosition;
        var held = m_GrabInteractable != null && m_GrabInteractable.isSelected;

        yield return AnimateGripRecoil(0f, 1f, kickDuration, gripPivot, held);
        yield return AnimateGripRecoil(1f, 0f, returnDuration, gripPivot, held);

        m_VisualRoot.SetLocalPositionAndRotation(m_VisualStartPosition, m_VisualStartRotation);
        if (m_FirePoint != null && m_FirePoint != transform)
            m_FirePoint.SetLocalPositionAndRotation(m_FireStartPosition, m_FireStartRotation);
        m_RecoilRoutine = null;
    }

    IEnumerator AnimateGripRecoil(float from, float to, float duration, Vector3 gripPivot, bool held)
    {
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var amount = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            var kick = Mathf.LerpUnclamped(from, to, amount);
            // Imported model origins can be metres from the visible weapon.
            // Rotate in the weapon frame at the grip throughout the animation;
            // interpolating model-origin positions would still pull the handle away.
            var pose = Primordia.WeaponPalmGrip.RecoilVisualPose(m_VisualStartPosition,
                m_VisualStartRotation, gripPivot, -m_RecoilAngle * kick);
            if (!held) pose.position -= Vector3.forward * (m_RecoilDistance * kick);
            m_VisualRoot.SetLocalPositionAndRotation(pose.position, pose.rotation);
            if (m_FirePoint != null && m_FirePoint.parent == m_VisualRoot.parent)
            {
                var muzzlePose = Primordia.WeaponPalmGrip.RecoilVisualPose(m_FireStartPosition,
                    m_FireStartRotation, gripPivot, -m_RecoilAngle * kick);
                if (!held) muzzlePose.position -= Vector3.forward * (m_RecoilDistance * kick);
                m_FirePoint.SetLocalPositionAndRotation(muzzlePose.position, muzzlePose.rotation);
            }
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
