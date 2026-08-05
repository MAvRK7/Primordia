using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;

/// <summary>
/// Shared comfort settings used by every locomotion vignette provider.
/// Create additional profiles when the game needs different comfort presets.
/// </summary>
[CreateAssetMenu(
    fileName = "Locomotion Comfort Profile",
    menuName = "Primordia/VR/Locomotion Comfort Profile")]
public sealed class LocomotionComfortProfile : ScriptableObject
{
    [SerializeField]
    [Tooltip("The first-run state. The player's saved preference takes priority after they change it.")]
    bool m_TunnelingVignetteEnabledByDefault;

    [SerializeField]
    VignetteParameters m_TunnelingVignette = new VignetteParameters();

    public bool tunnelingVignetteEnabledByDefault => m_TunnelingVignetteEnabledByDefault;

    public VignetteParameters tunnelingVignette => m_TunnelingVignette;
}
