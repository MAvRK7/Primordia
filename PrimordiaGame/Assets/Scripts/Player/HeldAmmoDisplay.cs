using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>Shows magazine ammo above a controller hand while that hand holds a gun.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public sealed class HeldAmmoDisplay : MonoBehaviour
{
    [Header("Player References")]
    [SerializeField] Transform m_DisplayAnchor;
    [SerializeField] Camera m_ViewCamera;

    [Header("Hand Placement")]
    [SerializeField] Vector3 m_HandOffset = new Vector3(0f, 0.1f, -0.02f);
    [SerializeField, Min(0.0001f)] float m_DisplayScale = 0.00065f;

    static readonly Color ReadyColor = new Color(0.9f, 0.94f, 0.98f);
    static readonly Color LowAmmoColor = new Color(1f, 0.76f, 0.24f);
    static readonly Color EmptyColor = new Color(0.98f, 0.26f, 0.26f);

    XRBaseInteractor[] m_Interactors;
    Ranged m_Gun;
    Canvas m_Canvas;
    TextMeshProUGUI m_Value;
    TextMeshProUGUI m_Status;
    int m_LastCurrent = -1;
    int m_LastMaximum = -1;
    int m_LastReserve = -1;
    bool m_LastReloading;

    public void Configure(Transform displayAnchor, Camera viewCamera)
    {
        m_DisplayAnchor = displayAnchor;
        m_ViewCamera = viewCamera;
    }

    void Awake()
    {
        // Only inspect this controller's interactors, so holstered guns and the
        // other hand cannot make its readout appear.
        m_Interactors = GetComponentsInChildren<XRBaseInteractor>(true);
        if (m_DisplayAnchor == null)
            m_DisplayAnchor = GetComponent<PlayerPunch>()?.PalmGripOrigin;
        if (m_DisplayAnchor == null) m_DisplayAnchor = transform;
        if (m_ViewCamera == null)
            m_ViewCamera = GetComponentInParent<PlayerHealth>()?.GetComponentInChildren<Camera>(true);
        if (m_ViewCamera == null || m_Interactors.Length == 0)
        {
            Debug.LogWarning("Held ammo display needs controller interactors and the XR camera.", this);
            enabled = false;
            return;
        }
        CreateDisplay();
    }

    void OnEnable() => Application.onBeforeRender += UpdatePose;

    void OnDisable()
    {
        Application.onBeforeRender -= UpdatePose;
        m_Gun = null;
        if (m_Canvas != null) m_Canvas.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (m_Canvas == null) return;
        var gun = FindHeldGun();
        var changedGun = gun != m_Gun;
        m_Gun = gun;
        if (m_Gun != null) UpdateAmmo(changedGun);
        UpdatePose();
    }

    Ranged FindHeldGun()
    {
        foreach (var interactor in m_Interactors)
        {
            if (interactor == null || !interactor.isActiveAndEnabled || interactor is XRSocketInteractor)
                continue;
            var selected = interactor.firstInteractableSelected;
            if (selected == null) continue;
            var gun = selected.transform.GetComponent<Ranged>();
            if (gun != null && gun.isActiveAndEnabled &&
                gun.Definition != null && gun.Definition.Type == WeaponType.Gun)
                return gun;
        }
        return null;
    }

    void UpdateAmmo(bool changedGun)
    {
        var maximum = Mathf.Max(0, m_Gun.Definition.MagazineCapacity);
        var current = Mathf.Clamp(m_Gun.CurrentMagazine, 0, maximum);
        var reserve = m_Gun.ReserveRounds;
        var reloading = m_Gun.IsReloading;
        if (!changedGun && current == m_LastCurrent && maximum == m_LastMaximum &&
            reserve == m_LastReserve && reloading == m_LastReloading)
            return;
        m_LastCurrent = current;
        m_LastMaximum = maximum;
        m_LastReserve = reserve;
        m_LastReloading = reloading;

        var lowAmmo = maximum > 0 && (float)current / maximum <= 0.25f;
        var color = current == 0 ? EmptyColor : lowAmmo ? LowAmmoColor : ReadyColor;
        m_Value.color = color;
        m_Value.SetText("{0:0} / {1:0}", current, maximum);
        m_Status.color = reloading ? ReadyColor : color;
        m_Status.text = reloading ? "RELOADING..."
            : current == 0 && reserve <= 0 ? "OUT OF AMMO"
            : current < maximum && reserve > 0 ? "B: RELOAD"
            : reserve <= 0 ? "NO RESERVE"
            : string.Empty;
    }

    // Match the tracked controller pose immediately before rendering in VR.
    [BeforeRenderOrder(120)]
    void UpdatePose()
    {
        if (m_Canvas == null) return;
        var visible = m_Gun != null && m_Gun.isActiveAndEnabled &&
            m_DisplayAnchor != null && m_DisplayAnchor.gameObject.activeInHierarchy &&
            m_ViewCamera != null && m_ViewCamera.isActiveAndEnabled;
        if (visible)
        {
            var position = m_DisplayAnchor.TransformPoint(m_HandOffset);
            var awayFromViewer = position - m_ViewCamera.transform.position;
            visible = awayFromViewer.sqrMagnitude > 0.0001f;
            if (visible)
                m_Canvas.transform.SetPositionAndRotation(position,
                    Quaternion.LookRotation(awayFromViewer, m_ViewCamera.transform.up));
        }
        if (m_Canvas.gameObject.activeSelf != visible)
            m_Canvas.gameObject.SetActive(visible);
    }

    void CreateDisplay()
    {
        var panel = new GameObject("Held Gun Ammo", typeof(RectTransform), typeof(Canvas), typeof(Image));
        panel.layer = 2; // Ignore Raycast: the readout cannot steal grab or UI input.
        panel.transform.SetParent(transform, false);
        panel.transform.localScale = Vector3.one * m_DisplayScale;
        var rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(176f, 96f);
        m_Canvas = panel.GetComponent<Canvas>();
        m_Canvas.renderMode = RenderMode.WorldSpace;
        m_Canvas.worldCamera = m_ViewCamera;
        var background = panel.GetComponent<Image>();
        background.color = new Color(0.035f, 0.046f, 0.06f, 0.94f);
        background.raycastTarget = false;

        var title = CreateText("Ammo Label", rect, new Vector2(152f, 22f), new Vector2(0f, 30f), 14f);
        title.text = "AMMO";
        m_Value = CreateText("Magazine Value", rect, new Vector2(152f, 38f), new Vector2(0f, 1f), 32f);
        m_Status = CreateText("Reload Status", rect, new Vector2(152f, 22f), new Vector2(0f, -30f), 16f);
        panel.SetActive(false);
    }

    static TextMeshProUGUI CreateText(string name, RectTransform parent, Vector2 size, Vector2 position, float fontSize)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        item.layer = 2;
        item.transform.SetParent(parent, false);
        var label = item.GetComponent<TextMeshProUGUI>();
        label.rectTransform.sizeDelta = size;
        label.rectTransform.anchoredPosition = position;
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.alignment = TextAlignmentOptions.Center;
        label.color = ReadyColor;
        label.raycastTarget = false;
        return label;
    }
}
