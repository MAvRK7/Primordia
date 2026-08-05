using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

/// <summary>
/// Applies the game's locomotion settings to the XR Interaction Toolkit rig and
/// returns the player to their spawn point if they leave the playable area.
///
/// The XR rig prefab owns the actual input and CharacterController movement. This
/// component deliberately configures that system instead of moving the rig's
/// Transform directly, which would bypass collision detection.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerRigController : MonoBehaviour
{
    const string k_TunnelingVignettePreference = "Primordia.Comfort.TunnelingVignette";

    [Header("Rig")]
    [SerializeField] XROrigin m_XROrigin;

    [Header("Locomotion")]
    [SerializeField, Min(0f)] float m_MoveSpeed = 2f;
    [SerializeField, Range(15f, 90f)] float m_SnapTurnAngle = 30f;

    [Header("Comfort")]
    [SerializeField] LocomotionComfortProfile m_ComfortProfile;

    [Header("Fall Recovery")]
    [SerializeField] bool m_ResetAfterFalling = true;
    [SerializeField] float m_FallResetHeight = -5f;

    Transform m_OriginTransform;
    CharacterController m_CharacterController;
    GravityProvider m_GravityProvider;
    TunnelingVignetteController m_TunnelingVignetteController;
    Vector3 m_SpawnPosition;
    Quaternion m_SpawnRotation;

    public bool tunnelingVignetteEnabled { get; private set; }

    void Awake()
    {
        if (m_XROrigin == null)
            m_XROrigin = FindFirstObjectByType<XROrigin>();

        if (m_XROrigin == null)
        {
            Debug.LogError("PlayerRigController could not find an XR Origin in the scene.", this);
            enabled = false;
            return;
        }

        m_OriginTransform = m_XROrigin.Origin != null
            ? m_XROrigin.Origin.transform
            : m_XROrigin.transform;

        m_SpawnPosition = m_OriginTransform.position;
        m_SpawnRotation = m_OriginTransform.rotation;

        m_CharacterController = m_OriginTransform.GetComponent<CharacterController>();
        m_GravityProvider = m_XROrigin.GetComponentInChildren<GravityProvider>(true);

        ConfigureLocomotion();
        ConfigureComfort();
    }

    void Update()
    {
        if (m_ResetAfterFalling && m_OriginTransform.position.y < m_FallResetHeight)
            ResetToSpawn();
    }

    void ConfigureLocomotion()
    {
        var moveProvider = m_XROrigin.GetComponentInChildren<ContinuousMoveProvider>(true);
        if (moveProvider != null)
        {
            moveProvider.moveSpeed = m_MoveSpeed;
            moveProvider.enableFly = false;
            moveProvider.enabled = true;
        }
        else
        {
            Debug.LogWarning("The XR Origin has no Continuous Move Provider.", m_XROrigin);
        }

        var snapTurnProvider = m_XROrigin.GetComponentInChildren<SnapTurnProvider>(true);
        if (snapTurnProvider != null)
            snapTurnProvider.turnAmount = m_SnapTurnAngle;

        if (m_CharacterController == null)
            Debug.LogError("The XR Origin needs a CharacterController for collision-aware movement.", m_XROrigin);
    }

    void ConfigureComfort()
    {
        m_TunnelingVignetteController =
            m_XROrigin.GetComponentInChildren<TunnelingVignetteController>(true);

        if (m_TunnelingVignetteController == null)
        {
            Debug.LogWarning("The XR Origin has no Tunneling Vignette Controller.", m_XROrigin);
            return;
        }

        if (m_ComfortProfile != null)
        {
            m_TunnelingVignetteController.defaultParameters.CopyFrom(
                m_ComfortProfile.tunnelingVignette);
        }

        // All entries use the controller's shared default parameters. This removes
        // per-provider comfort settings while preserving which locomotion providers
        // are registered by the XR template.
        foreach (var provider in m_TunnelingVignetteController.locomotionVignetteProviders)
            provider.overrideDefaultParameters = false;

        var enabledByDefault = m_ComfortProfile != null &&
            m_ComfortProfile.tunnelingVignetteEnabledByDefault;
        var savedValue = PlayerPrefs.GetInt(
            k_TunnelingVignettePreference,
            enabledByDefault ? 1 : 0);

        ApplyTunnelingVignetteEnabled(savedValue != 0);
    }

    /// <summary>
    /// Enables or disables the shared locomotion vignette and saves the player's choice.
    /// This can be connected directly to a UI Toggle's On Value Changed event.
    /// </summary>
    public void SetTunnelingVignetteEnabled(bool enabled)
    {
        ApplyTunnelingVignetteEnabled(enabled);
        PlayerPrefs.SetInt(k_TunnelingVignettePreference, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    void ApplyTunnelingVignetteEnabled(bool enabled)
    {
        tunnelingVignetteEnabled = enabled;

        if (m_TunnelingVignetteController == null)
            return;

        foreach (var provider in m_TunnelingVignetteController.locomotionVignetteProviders)
        {
            // If the option is disabled while the effect is visible, queue its
            // normal ease-out before preventing future locomotion triggers.
            if (!enabled && provider.enabled)
                m_TunnelingVignetteController.EndTunnelingVignette(provider);

            provider.enabled = enabled;
        }
    }

    /// <summary>Returns the XR Origin to the position and rotation it had at scene startup.</summary>
    public void ResetToSpawn()
    {
        if (m_OriginTransform == null)
            return;

        var gravityWasEnabled = m_GravityProvider != null && m_GravityProvider.enabled;
        if (gravityWasEnabled)
            m_GravityProvider.enabled = false;

        var controllerWasEnabled = m_CharacterController != null && m_CharacterController.enabled;
        if (controllerWasEnabled)
            m_CharacterController.enabled = false;

        m_OriginTransform.SetPositionAndRotation(m_SpawnPosition, m_SpawnRotation);
        Physics.SyncTransforms();

        if (controllerWasEnabled)
            m_CharacterController.enabled = true;

        if (gravityWasEnabled)
            m_GravityProvider.enabled = true;
    }
}
