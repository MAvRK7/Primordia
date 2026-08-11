using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Primordia
{
    /// <summary>
    /// Animates a serialized controller-hand prefab from grip and trigger input.
    /// Controller tracking remains the pose source; this component only curls fingers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ControllerHandVisual : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer m_HandRenderer;
        [SerializeField] private bool m_IsLeftHand;
        [SerializeField] private InputActionReference m_GripAction;
        [SerializeField] private InputActionReference m_TriggerAction;
        [SerializeField, Min(0f)] private float m_SmoothingSpeed = 18f;

        private readonly Dictionary<string, BonePose> m_Bones = new();
        private float m_Grip;
        private float m_Trigger;

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
        }

        private void Update()
        {
            var targetGrip = ReadValue(m_GripAction);
            var targetTrigger = ReadValue(m_TriggerAction);
            var blend = m_SmoothingSpeed <= 0f
                ? 1f
                : 1f - Mathf.Exp(-m_SmoothingSpeed * Time.unscaledDeltaTime);
            m_Grip = Mathf.Lerp(m_Grip, targetGrip, blend);
            m_Trigger = Mathf.Lerp(m_Trigger, targetTrigger, blend);

            var prefix = m_IsLeftHand ? "L_" : "R_";
            SetFingerCurl(prefix, "Index", Mathf.Max(m_Trigger, m_Grip * 0.25f));
            SetFingerCurl(prefix, "Middle", m_Grip);
            SetFingerCurl(prefix, "Ring", m_Grip);
            SetFingerCurl(prefix, "Little", m_Grip);
            SetThumbCurl(prefix, m_Grip);
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
