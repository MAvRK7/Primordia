using System.Linq;
using UnityEngine;

/// <summary>
/// Keeps body inventory sockets on an estimated torso instead of at fixed XR-origin offsets.
/// Head pitch and roll are ignored so crouching and looking around do not tip the holsters.
/// </summary>
[DisallowMultipleComponent]
public sealed class BodyInventoryRig : MonoBehaviour
{
    [Header("Tracking")]
    [SerializeField] Transform m_Head;
    [SerializeField, Min(0f)] float m_TurnSmoothing = 12f;

    [Header("Sockets")]
    [SerializeField] Transform m_BackLeft;
    [SerializeField] Transform m_BackRight;
    [SerializeField] Transform m_HipLeft;
    [SerializeField] Transform m_HipRight;

    [Header("Body Measurements")]
    [SerializeField, Min(0f)] float m_ShoulderHalfWidth = 0.24f;
    [SerializeField, Min(0f)] float m_ShoulderDrop = 0.25f;
    [SerializeField] float m_ShoulderForwardOffset = -0.11f;
    [SerializeField, Min(0f)] float m_HipHalfWidth = 0.19f;
    [SerializeField, Min(0f)] float m_HipDrop = 0.72f;
    [SerializeField] float m_HipForwardOffset = 0.02f;
    [SerializeField] Vector3 m_HolsteredEulerAngles = new Vector3(90f, 0f, 0f);

    Vector3 m_BodyForward;
    bool m_HasPose;

    public Transform Head => m_Head;

    void Reset()
    {
        FindReferences();
    }

    void Awake()
    {
        FindReferences();
        SnapToBody();
    }

    void OnEnable()
    {
        m_HasPose = false;
    }

    void LateUpdate()
    {
        UpdateBodyPose(false);
    }

    public void ConfigureFromHierarchy(Transform head)
    {
        m_Head = head;
        FindSocketReferences();
        ApplySocketOffsets();
    }

    public void SnapToBody()
    {
        UpdateBodyPose(true);
    }

    void UpdateBodyPose(bool snap)
    {
        if (m_Head == null)
            FindReferences();
        if (m_Head == null)
            return;

        var targetForward = Vector3.ProjectOnPlane(m_Head.forward, Vector3.up);
        if (targetForward.sqrMagnitude < 0.001f)
            targetForward = m_HasPose ? m_BodyForward : transform.forward;
        targetForward.Normalize();

        if (snap || !m_HasPose || m_TurnSmoothing <= 0f)
        {
            m_BodyForward = targetForward;
        }
        else
        {
            var blend = 1f - Mathf.Exp(-m_TurnSmoothing * Time.unscaledDeltaTime);
            m_BodyForward = Vector3.Slerp(m_BodyForward, targetForward, blend).normalized;
        }

        transform.SetPositionAndRotation(
            m_Head.position,
            Quaternion.LookRotation(m_BodyForward, Vector3.up));
        ApplySocketOffsets();
        m_HasPose = true;
    }

    void ApplySocketOffsets()
    {
        var holsteredRotation = Quaternion.Euler(m_HolsteredEulerAngles);
        SetSocketPose(
            m_BackLeft,
            new Vector3(-m_ShoulderHalfWidth, -m_ShoulderDrop, m_ShoulderForwardOffset),
            holsteredRotation);
        SetSocketPose(
            m_BackRight,
            new Vector3(m_ShoulderHalfWidth, -m_ShoulderDrop, m_ShoulderForwardOffset),
            holsteredRotation);
        SetSocketPose(
            m_HipLeft,
            new Vector3(-m_HipHalfWidth, -m_HipDrop, m_HipForwardOffset),
            holsteredRotation);
        SetSocketPose(
            m_HipRight,
            new Vector3(m_HipHalfWidth, -m_HipDrop, m_HipForwardOffset),
            holsteredRotation);
    }

    static void SetSocketPose(Transform socket, Vector3 localPosition, Quaternion localRotation)
    {
        if (socket == null)
            return;

        socket.SetLocalPositionAndRotation(localPosition, localRotation);
    }

    void FindReferences()
    {
        if (m_Head == null)
        {
            var playerCamera = GetComponentInParent<Camera>();
            if (playerCamera == null)
                playerCamera = Camera.main;
            if (playerCamera != null)
                m_Head = playerCamera.transform;
        }

        FindSocketReferences();
    }

    void FindSocketReferences()
    {
        var children = GetComponentsInChildren<Transform>(true);
        m_BackLeft ??= FindChild(children, "BackLeft");
        m_BackRight ??= FindChild(children, "BackRight");
        m_HipLeft ??= FindChild(children, "HipLeft");
        m_HipRight ??= FindChild(children, "HipRight");
    }

    static Transform FindChild(Transform[] children, string childName)
    {
        return children.FirstOrDefault(child => child.name == childName);
    }

    void OnValidate()
    {
        m_TurnSmoothing = Mathf.Max(0f, m_TurnSmoothing);
        m_ShoulderHalfWidth = Mathf.Max(0f, m_ShoulderHalfWidth);
        m_ShoulderDrop = Mathf.Max(0f, m_ShoulderDrop);
        m_HipHalfWidth = Mathf.Max(0f, m_HipHalfWidth);
        m_HipDrop = Mathf.Max(0f, m_HipDrop);
        FindSocketReferences();
        ApplySocketOffsets();
    }
}
