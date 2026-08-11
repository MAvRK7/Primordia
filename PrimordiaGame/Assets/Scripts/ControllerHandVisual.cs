using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Primordia
{
    /// <summary>
    /// Replaces a tracked-controller model with an animated skinned hand.
    /// The controller remains the pose source; grip and trigger drive finger curl.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ControllerHandVisual : MonoBehaviour
    {
        [Header("Model")]
        [SerializeField] private GameObject m_HandModel;
        [SerializeField] private Transform m_LegacyControllerModel;
        [SerializeField] private Material m_HandMaterial;
        [SerializeField] private bool m_IsLeftHand;
        [SerializeField] private Vector3 m_ModelLocalPosition;
        [SerializeField] private Vector3 m_ModelLocalEulerAngles;
        [SerializeField] private Vector3 m_ModelLocalScale = Vector3.one;

        [Header("Input")]
        [SerializeField] private InputActionReference m_GripAction;
        [SerializeField] private InputActionReference m_TriggerAction;
        [SerializeField, Min(0f)] private float m_SmoothingSpeed = 18f;

        private readonly Dictionary<string, BonePose> m_Bones = new();
        private GameObject m_Instance;
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
            if (m_HandModel == null)
            {
                Debug.LogError("Controller hand visual has no hand model.", this);
                return;
            }

            m_Instance = Instantiate(m_HandModel, transform, false);
            m_Instance.name = m_IsLeftHand
                ? "Left Controller Hand"
                : "Right Controller Hand";
            m_Instance.transform.SetLocalPositionAndRotation(
                m_ModelLocalPosition,
                Quaternion.Euler(m_ModelLocalEulerAngles));
            m_Instance.transform.localScale = m_ModelLocalScale;

            var handRenderer = m_Instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (handRenderer == null)
            {
                Debug.LogError("Controller hand model has no skinned renderer.", this);
                Destroy(m_Instance);
                m_Instance = null;
                return;
            }

            foreach (var modelRenderer in m_Instance.GetComponentsInChildren<Renderer>(true))
                modelRenderer.enabled = modelRenderer == handRenderer;
            foreach (var modelCamera in m_Instance.GetComponentsInChildren<Camera>(true))
                modelCamera.enabled = false;
            foreach (var modelLight in m_Instance.GetComponentsInChildren<Light>(true))
                modelLight.enabled = false;
            if (m_HandMaterial != null)
                handRenderer.sharedMaterial = m_HandMaterial;

            var prefix = m_IsLeftHand ? "L_" : "R_";
            foreach (var bone in m_Instance.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name.StartsWith(prefix))
                    m_Bones[bone.name] = new BonePose(bone);
            }

            if (m_Bones.Count != 26)
            {
                Debug.LogError(
                    $"Controller hand model exposed {m_Bones.Count} XR bones; expected 26.",
                    this);
                Destroy(m_Instance);
                m_Instance = null;
                m_Bones.Clear();
                return;
            }

            if (m_LegacyControllerModel != null)
                m_LegacyControllerModel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (m_Instance == null)
                return;

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

        private void OnDestroy()
        {
            if (m_Instance != null)
                Destroy(m_Instance);
        }
    }
}
