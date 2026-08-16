using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Animation-driven melee weapon with a short, explicit damage window.
/// Each target can only be damaged once per swing.
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

    [Header("Swing Timing")]
    [SerializeField, Min(0f)] float m_WindupDuration = 0.05f;
    [SerializeField, Min(0.01f)] float m_ActiveDuration = 0.18f;

    [Header("Feedback")]
    [SerializeField] Animator m_Animator;
    [SerializeField] string m_SwingTrigger = "Swing";
    [SerializeField] AudioSource m_AudioSource;
    [SerializeField] AudioClip m_SwingClip;
    [SerializeField] AudioClip m_HitClip;
    [SerializeField, Range(0f, 1f)] float m_HitHapticAmplitude = 0.25f;
    [SerializeField, Min(0f)] float m_HitHapticDuration = 0.04f;

    readonly HashSet<IDamageable> m_HitTargets = new HashSet<IDamageable>();

    bool m_IsSwinging;
    bool m_DamageWindowOpen;
    float m_NextSwingTime;
    Transform m_Attacker;
    XRBaseInputInteractor m_ActiveInteractor;

    public WeaponDefinition Definition => m_Definition;
    public bool IsSwinging => m_IsSwinging;
    public bool DamageWindowOpen => m_DamageWindowOpen;

    void Awake()
    {
        if (m_GrabInteractable == null)
            m_GrabInteractable = GetComponent<XRGrabInteractable>();

        if (m_AudioSource == null)
            m_AudioSource = GetComponent<AudioSource>();

        SetDamageWindow(false);
    }

    void OnEnable()
    {
        if (m_GrabInteractable != null)
            m_GrabInteractable.activated.AddListener(HandleActivated);
    }

    void OnDisable()
    {
        if (m_GrabInteractable != null)
            m_GrabInteractable.activated.RemoveListener(HandleActivated);

        StopAllCoroutines();
        m_IsSwinging = false;
        m_ActiveInteractor = null;
        m_HitTargets.Clear();
        SetDamageWindow(false);
    }

    void HandleActivated(ActivateEventArgs args)
    {
        BeginSwing(
            args.interactorObject.transform,
            args.interactorObject as XRBaseInputInteractor);
    }

    public bool BeginSwing(Transform attacker = null)
    {
        return BeginSwing(attacker, null);
    }

    bool BeginSwing(Transform attacker, XRBaseInputInteractor interactor)
    {
        if (m_Definition == null ||
            m_Definition.Type != WeaponType.Melee ||
            m_IsSwinging ||
            Time.time < m_NextSwingTime)
        {
            return false;
        }

        m_Attacker = attacker != null ? attacker : transform;
        m_ActiveInteractor = interactor;
        m_NextSwingTime = Time.time + 1f / m_Definition.AttacksPerSecond;
        StartCoroutine(SwingRoutine());
        return true;
    }

    IEnumerator SwingRoutine()
    {
        m_IsSwinging = true;
        m_HitTargets.Clear();

        if (m_Animator != null && !string.IsNullOrWhiteSpace(m_SwingTrigger))
            m_Animator.SetTrigger(m_SwingTrigger);

        PlayOneShot(m_SwingClip);
        PlayerNoise.EmitNoise(transform.position, m_Definition.NoiseRange);

        if (m_WindupDuration > 0f)
            yield return new WaitForSeconds(m_WindupDuration);

        SetDamageWindow(true);
        yield return new WaitForSeconds(m_ActiveDuration);
        SetDamageWindow(false);

        m_IsSwinging = false;
        m_ActiveInteractor = null;
    }

    internal void RegisterHit(Collider other, Collider sourceCollider)
    {
        if (!m_DamageWindowOpen || other == null || other.transform.IsChildOf(transform))
            return;

        if (!CombatDamageResolver.TryGetDamageable(other, out var target) ||
            m_HitTargets.Contains(target))
        {
            return;
        }

        m_HitTargets.Add(target);

        var origin = sourceCollider != null
            ? sourceCollider.bounds.center
            : transform.position;
        var point = other.ClosestPoint(origin);
        var normal = point - origin;
        if (normal.sqrMagnitude <= Mathf.Epsilon)
            normal = -transform.forward;

        target.ApplyHit(new CombatHit(
            m_Definition.Damage,
            point,
            normal,
            m_Attacker));

        PlayOneShot(m_HitClip);
        m_ActiveInteractor?.SendHapticImpulse(m_HitHapticAmplitude, m_HitHapticDuration);
    }

    void SetDamageWindow(bool open)
    {
        m_DamageWindowOpen = open;
        if (m_Hitbox != null)
            m_Hitbox.SetActive(open);
    }

    void PlayOneShot(AudioClip clip)
    {
        if (m_AudioSource != null && clip != null)
            m_AudioSource.PlayOneShot(clip);
    }

    /// <summary>
    /// Animation events can call this when a custom weapon animation controls timing.
    /// </summary>
    public void OpenDamageWindow()
    {
        if (m_IsSwinging)
            SetDamageWindow(true);
    }

    /// <summary>
    /// Animation events can call this when a custom weapon animation controls timing.
    /// </summary>
    public void CloseDamageWindow()
    {
        SetDamageWindow(false);
    }

    void OnValidate()
    {
        m_WindupDuration = Mathf.Max(0f, m_WindupDuration);
        m_ActiveDuration = Mathf.Max(0.01f, m_ActiveDuration);
        m_HitHapticDuration = Mathf.Max(0f, m_HitHapticDuration);
    }
}
