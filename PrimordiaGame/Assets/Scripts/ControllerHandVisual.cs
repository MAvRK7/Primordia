using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Primordia
{
    /// <summary>
    /// Animates a serialized controller-hand prefab from grip and trigger input.
    /// Keeps the palm at the grip position and points the fingers along the aim pose.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ControllerHandVisual : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer m_HandRenderer;
        [SerializeField] private bool m_IsLeftHand;
        [SerializeField] private InputActionReference m_GripAction;
        [SerializeField] private InputActionReference m_TriggerAction;
        [SerializeField, Min(0f)] private float m_SmoothingSpeed = 18f;
        [SerializeField] private Transform m_AimRotationSource;
        [SerializeField] private Transform m_PalmGripAnchor;

        private readonly Dictionary<string, BonePose> m_Bones = new();
        private float m_Grip;
        private float m_Trigger;
        private float m_HeldRingCurlScale = 0.9f;
        private float m_HeldLittleCurlScale = 0.75f;
        private float m_HeldThumbCurlScale = 1f;
        private float m_HeldHandPitch;
        private Vector3 m_HeldGripFingerAngles = new Vector3(35f, 55f, 40f);
        private float m_HeldIndexCurlScale = 1f;
        private Vector2 m_HeldThumbOpposition;
        private Quaternion m_AimRotationOffset = Quaternion.identity;
        private PlayerPunch m_PlayerPunch;
        private XRBaseInteractor[] m_Interactors;

        // The palm bone lies in the back of the hand. Handles belong in the
        // channel enclosed by the middle/ring/little fingers, in the aim frame.
        public static Vector3 PalmGripOffset(bool isLeftHand) =>
            new Vector3(isLeftHand ? 0.0285f : -0.0285f, -0.003f, 0.043f);

        private readonly struct BonePose
        {
            public BonePose(Transform transform)
            {
                Transform = transform;
                RestRotation = transform.localRotation;
            }

            public Transform Transform { get; }
            public Quaternion RestRotation { get; }
        }

        private void Awake()
        {
            m_PlayerPunch = GetComponentInParent<PlayerPunch>();
            m_Interactors = m_PlayerPunch != null
                ? m_PlayerPunch.GetComponentsInChildren<XRBaseInteractor>(true)
                : new XRBaseInteractor[0];
            if (m_PalmGripAnchor == null) m_PalmGripAnchor = m_PlayerPunch?.PalmGripOrigin;
            if (m_AimRotationSource == null)
                m_AimRotationSource = m_PlayerPunch?.AimOrigin;
            if (m_HandRenderer == null)
                m_HandRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (m_HandRenderer == null)
            {
                Debug.LogError("Controller hand prefab has no skinned renderer.", this);
                enabled = false;
                return;
            }

            var prefix = m_IsLeftHand ? "L_" : "R_";
            foreach (var bone in m_HandRenderer.bones)
            {
                if (bone != null && bone.name.StartsWith(prefix))
                    m_Bones[bone.name] = new BonePose(bone);
            }

            if (m_Bones.Count != 26)
            {
                Debug.LogError(
                    $"Controller hand prefab exposed {m_Bones.Count} XR bones; expected 26.",
                    this);
                enabled = false;
            }
            else if (m_Bones.TryGetValue(prefix + "Palm", out var palm))
            {
                // The palm frame is stable; a relaxed fingertip already bends
                // downward and must not tilt the whole wrist during calibration.
                var palmRotation = Quaternion.Inverse(transform.rotation) * palm.Transform.rotation;
                m_AimRotationOffset = Quaternion.Inverse(palmRotation);
            }
        }

        private void OnEnable() => Application.onBeforeRender += AlignHand;

        private void OnDisable() => Application.onBeforeRender -= AlignHand;

        private void LateUpdate() => AlignHand();

        [BeforeRenderOrder(100)]
        private void AlignHand()
        {
            if (m_AimRotationSource != null)
            {
                // Both source meshes have their palms facing -Y. Roll each wrist
                // toward the other hand while keeping the thumbs up and aim +Z.
                var wristRoll = Quaternion.AngleAxis(m_IsLeftHand ? 90f : -90f, Vector3.forward);
                var heldPitch = Quaternion.AngleAxis(m_HeldHandPitch, Vector3.right);
                transform.rotation = m_AimRotationSource.rotation * heldPitch * wristRoll * m_AimRotationOffset;
            }
        }

        private void Update()
        {
            // Butchering requires released grip input. Close the visual hand
            // around its temporary knife without changing that input gesture.
            var knifeHeld = m_PlayerPunch != null && m_PlayerPunch.IsHoldingButcherKnife;
            var heldGrip = HeldGrip();
            if (heldGrip == null && knifeHeld) heldGrip = m_PlayerPunch.ButcherGrip;
            // Selection can latch before the analog grip reaches one. Keep the
            // authored grasp closed for the whole time the weapon is selected.
            var targetGrip = heldGrip != null || knifeHeld ? 1f : ReadValue(m_GripAction);
            var targetTrigger = knifeHeld ? 1f : ReadValue(m_TriggerAction);
            var curlLimit = heldGrip != null ? heldGrip.FingerCurl : 1f;
            m_HeldRingCurlScale = heldGrip != null ? heldGrip.RingCurlScale : 0.9f;
            m_HeldLittleCurlScale = heldGrip != null ? heldGrip.LittleCurlScale : 0.75f;
            m_HeldThumbCurlScale = heldGrip != null ? heldGrip.ThumbCurlScale : 1f;
            m_HeldHandPitch = heldGrip != null ? heldGrip.HandPitch : 0f;
            m_HeldGripFingerAngles = heldGrip != null ? heldGrip.GripFingerAngles : new Vector3(35f, 55f, 40f);
            m_HeldIndexCurlScale = heldGrip != null ? heldGrip.IndexCurlScale : 1f;
            m_HeldThumbOpposition = heldGrip != null ? heldGrip.ThumbOpposition : Vector2.zero;
            if (m_PalmGripAnchor != null)
                m_PalmGripAnchor.localPosition = heldGrip != null
                    ? heldGrip.GetPalmOffset(m_IsLeftHand) : PalmGripOffset(m_IsLeftHand);
            targetGrip *= curlLimit;
            targetTrigger *= curlLimit;
            var blend = m_SmoothingSpeed <= 0f
                ? 1f
                : 1f - Mathf.Exp(-m_SmoothingSpeed * Time.unscaledDeltaTime);
            m_Grip = Mathf.Lerp(m_Grip, targetGrip, blend);
            m_Trigger = Mathf.Lerp(m_Trigger, targetTrigger, blend);

            ApplyFingerPose(heldGrip != null || knifeHeld);
        }

        private void ApplyFingerPose(bool holdingItem)
        {
            var prefix = m_IsLeftHand ? "L_" : "R_";
            SetFingerCurl(prefix, "Index", Mathf.Max(m_Trigger * (holdingItem ? m_HeldIndexCurlScale : 1f), m_Grip * 0.25f));
            SetGrippingFingerCurl(prefix, "Middle", m_Grip, holdingItem);
            // The shorter fingers close sooner around a handle. Applying the
            // punching curl to every finger pushed ring/little through the shaft.
            SetGrippingFingerCurl(prefix, "Ring", m_Grip * (holdingItem ? m_HeldRingCurlScale : 1f), holdingItem);
            SetGrippingFingerCurl(prefix, "Little", m_Grip * (holdingItem ? m_HeldLittleCurlScale : 1f), holdingItem);
            SetThumbCurl(prefix, m_Grip * (holdingItem ? m_HeldThumbCurlScale : 1f));
            if (holdingItem && m_Bones.TryGetValue(prefix + "ThumbMetacarpal", out var thumb))
            {
                var opposition = m_HeldThumbOpposition * (m_Grip * (m_IsLeftHand ? -1f : 1f));
                thumb.Transform.localRotation *= Quaternion.AngleAxis(opposition.x, Vector3.up) *
                    Quaternion.AngleAxis(opposition.y, Vector3.forward);
            }
        }

        private WeaponPalmGrip HeldGrip()
        {
            foreach (var interactor in m_Interactors)
            {
                var selected = interactor.firstInteractableSelected;
                if (selected == null) continue;
                var grip = selected.transform.GetComponent<WeaponPalmGrip>();
                if (grip != null) return grip;
            }
            return null;
        }

        private void SetFingerCurl(string prefix, string finger, float curl)
        {
            SetBoneCurl($"{prefix}{finger}Proximal", 35f * curl);
            SetBoneCurl($"{prefix}{finger}Intermediate", 55f * curl);
            SetBoneCurl($"{prefix}{finger}Distal", 40f * curl);
        }

        private void SetThumbCurl(string prefix, float curl)
        {
            SetBoneCurl($"{prefix}ThumbMetacarpal", 12f * curl);
            SetBoneCurl($"{prefix}ThumbProximal", 28f * curl);
            SetBoneCurl($"{prefix}ThumbDistal", 24f * curl);
        }

        private void SetGrippingFingerCurl(string prefix, string finger, float curl, bool holdingItem)
        {
            if (!holdingItem)
            {
                SetFingerCurl(prefix, finger, curl);
                return;
            }
            SetBoneCurl($"{prefix}{finger}Proximal", m_HeldGripFingerAngles.x * curl);
            SetBoneCurl($"{prefix}{finger}Intermediate", m_HeldGripFingerAngles.y * curl);
            SetBoneCurl($"{prefix}{finger}Distal", m_HeldGripFingerAngles.z * curl);
        }

        private void SetBoneCurl(string boneName, float angle)
        {
            if (!m_Bones.TryGetValue(boneName, out var bone))
                return;
            bone.Transform.localRotation =
                bone.RestRotation * Quaternion.AngleAxis(angle, Vector3.right);
        }

        private static float ReadValue(InputActionReference actionReference)
        {
            var action = actionReference != null ? actionReference.action : null;
            return action != null && action.enabled
                ? Mathf.Clamp01(action.ReadValue<float>())
                : 0f;
        }
    }
}
