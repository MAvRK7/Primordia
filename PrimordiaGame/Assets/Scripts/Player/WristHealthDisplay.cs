using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A small, non-interactive health readout that follows the visible controller wrist.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public sealed class WristHealthDisplay : MonoBehaviour
{
    [Header("Player References")]
    [SerializeField] PlayerHealth m_Health;
    [SerializeField] Transform m_Hand;
    [SerializeField] Camera m_ViewCamera;

    [Header("Wrist Placement")]
    [SerializeField] Vector3 m_WristOffset = new Vector3(0f, 0f, -0.025f);
    [SerializeField, Min(0f)] float m_SurfaceOffset = 0.035f;
    [SerializeField, Min(0.0001f)] float m_DisplayScale = 0.00065f;

    const float BarWidth = 148f;
    static readonly Color HealthyColor = new Color(0.22f, 0.87f, 0.48f);
    static readonly Color WarningColor = new Color(1f, 0.76f, 0.24f);
    static readonly Color CriticalColor = new Color(0.98f, 0.26f, 0.26f);

    Transform m_Wrist;
    Canvas m_Canvas;
    Image m_Fill;
    TextMeshProUGUI m_Value;
    float m_LastHealth = float.NaN;
    float m_LastMaximum = float.NaN;

    public void Configure(PlayerHealth health, Transform hand, Camera viewCamera)
    {
        m_Health = health;
        m_Hand = hand;
        m_ViewCamera = viewCamera;
    }

    void Awake()
    {
        if (m_Health == null) m_Health = GetComponentInParent<PlayerHealth>();
        if (m_ViewCamera == null && m_Health != null)
            m_ViewCamera = m_Health.GetComponentInChildren<Camera>(true);
        if (m_Hand != null)
        {
            foreach (var bone in m_Hand.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name != "L_Wrist" && bone.name != "R_Wrist") continue;
                m_Wrist = bone;
                break;
            }
        }
        if (m_Health == null || m_Wrist == null || m_ViewCamera == null)
        {
            Debug.LogWarning("Wrist health display needs player health, a controller hand with a wrist bone, and the XR camera.", this);
            enabled = false;
            return;
        }
        CreateDisplay();
    }

    void OnEnable()
    {
        Application.onBeforeRender += UpdatePose;
    }

    void OnDisable()
    {
        Application.onBeforeRender -= UpdatePose;
        if (m_Canvas != null) m_Canvas.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (m_Canvas == null) return;
        UpdateHealth();
        UpdatePose();
    }

    void UpdateHealth()
    {
        var maximum = Mathf.Max(0f, m_Health.maxHealth);
        var current = Mathf.Clamp(m_Health.CurrentHealth, 0f, maximum);
        if (current == m_LastHealth && maximum == m_LastMaximum) return;
        m_LastHealth = current;
        m_LastMaximum = maximum;

        var fraction = maximum > 0f ? current / maximum : 0f;
        m_Fill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, BarWidth * fraction);
        m_Fill.enabled = fraction > 0f;
        m_Fill.color = fraction > 0.5f ? HealthyColor : fraction > 0.25f ? WarningColor : CriticalColor;
        m_Value.color = m_Fill.color;
        m_Value.SetText("{0:0} / {1:0}", Mathf.Ceil(current), Mathf.Ceil(maximum));
    }

    // ControllerHandVisual aligns the wrist at order 100, after the tracked poses.
    [BeforeRenderOrder(120)]
    void UpdatePose()
    {
        if (m_Canvas == null || m_Wrist == null || m_ViewCamera == null) return;
        var visible = m_Wrist.gameObject.activeInHierarchy && m_ViewCamera.isActiveAndEnabled;
        m_Canvas.gameObject.SetActive(visible);
        if (!visible) return;

        var position = m_Wrist.TransformPoint(m_WristOffset);
        var towardViewer = m_ViewCamera.transform.position - position;
        if (towardViewer.sqrMagnitude < 0.0001f) return;
        position += towardViewer.normalized * m_SurfaceOffset;
        m_Canvas.transform.SetPositionAndRotation(position,
            Quaternion.LookRotation(position - m_ViewCamera.transform.position, m_ViewCamera.transform.up));
    }

    void CreateDisplay()
    {
        var panel = new GameObject("Wrist Health", typeof(RectTransform), typeof(Canvas));
        panel.layer = 2; // Ignore Raycast: this display cannot steal weapon or UI interaction.
        panel.transform.SetParent(transform, false);
        panel.transform.localScale = Vector3.one * m_DisplayScale;
        var rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(176f, 68f);
        m_Canvas = panel.GetComponent<Canvas>();
        m_Canvas.renderMode = RenderMode.WorldSpace;
        m_Canvas.worldCamera = m_ViewCamera;

        CreateImage("Panel", rect, new Vector2(176f, 68f), Vector2.zero,
            new Color(0.035f, 0.046f, 0.06f, 0.94f));
        var title = CreateText("Health Label", rect, new Vector2(82f, 24f), new Vector2(-35f, 17f), 14f);
        title.alignment = TextAlignmentOptions.Left;
        title.text = "HEALTH";
        m_Value = CreateText("Health Value", rect, new Vector2(88f, 24f), new Vector2(32f, 17f), 20f);
        m_Value.alignment = TextAlignmentOptions.Right;

        var track = CreateImage("Health Track", rect, new Vector2(152f, 14f), new Vector2(0f, -13f),
            new Color(0.16f, 0.19f, 0.22f));
        m_Fill = CreateImage("Health Fill", track.rectTransform, new Vector2(BarWidth, 10f), Vector2.zero, HealthyColor);
        var fillRect = m_Fill.rectTransform;
        fillRect.anchorMin = fillRect.anchorMax = new Vector2(0f, 0.5f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = new Vector2(2f, 0f);
    }

    static Image CreateImage(string name, RectTransform parent, Vector2 size, Vector2 position, Color color)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(Image));
        item.layer = 2;
        item.transform.SetParent(parent, false);
        var image = item.GetComponent<Image>();
        image.rectTransform.sizeDelta = size;
        image.rectTransform.anchoredPosition = position;
        image.color = color;
        image.raycastTarget = false;
        return image;
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
        label.color = new Color(0.9f, 0.94f, 0.98f);
        label.raycastTarget = false;
        return label;
    }
}
