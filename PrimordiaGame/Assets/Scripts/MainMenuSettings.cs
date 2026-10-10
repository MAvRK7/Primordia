using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Uses the main menu's existing world-space canvas and XR button styling.</summary>
[DisallowMultipleComponent]
public sealed class MainMenuSettings : MonoBehaviour
{
    [SerializeField] RectTransform m_MenuColumn;
    [SerializeField] Button m_SettingsButton;
    [SerializeField] Button m_ButtonTemplate;

    RectTransform m_Panel;
    Button m_InvulnerabilityButton;
    TMP_Text m_InvulnerabilityLabel;

    void Awake()
    {
        if (m_MenuColumn == null || m_SettingsButton == null || m_ButtonTemplate == null)
        {
            Debug.LogWarning("Main menu settings needs the menu column, Settings button and button template.", this);
            enabled = false;
            return;
        }
        CreatePanel();
        m_SettingsButton.onClick.AddListener(Open);
    }

    void OnEnable()
    {
        DevelopmentSettings.InvulnerabilityChanged += RefreshInvulnerability;
        RefreshInvulnerability(DevelopmentSettings.InvulnerabilityEnabled);
    }

    void OnDisable()
    {
        DevelopmentSettings.InvulnerabilityChanged -= RefreshInvulnerability;
    }

    void OnDestroy()
    {
        if (m_SettingsButton != null) m_SettingsButton.onClick.RemoveListener(Open);
        if (m_Panel != null) Destroy(m_Panel.gameObject);
    }

    public void Open()
    {
        RefreshInvulnerability(DevelopmentSettings.InvulnerabilityEnabled);
        m_MenuColumn.gameObject.SetActive(false);
        m_Panel.gameObject.SetActive(true);
        m_InvulnerabilityButton.Select();
    }

    void Close()
    {
        m_Panel.gameObject.SetActive(false);
        m_MenuColumn.gameObject.SetActive(true);
        m_SettingsButton.Select();
    }

    void ToggleInvulnerability()
    {
        DevelopmentSettings.InvulnerabilityEnabled = !DevelopmentSettings.InvulnerabilityEnabled;
    }

    void RefreshInvulnerability(bool enabled)
    {
        if (m_InvulnerabilityLabel == null) return;
        m_InvulnerabilityLabel.text = "Invulnerability (dev)\n" + (enabled ? "ON" : "OFF");
        m_InvulnerabilityButton.image.color = enabled
            ? new Color(0.65f, 0.95f, 0.73f)
            : new Color(0.9f, 0.92f, 0.95f);
    }

    void CreatePanel()
    {
        var panel = new GameObject("Settings Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.layer = m_MenuColumn.gameObject.layer;
        m_Panel = panel.GetComponent<RectTransform>();
        m_Panel.SetParent(m_MenuColumn.parent, false);
        m_Panel.anchorMin = m_MenuColumn.anchorMin;
        m_Panel.anchorMax = m_MenuColumn.anchorMax;
        m_Panel.pivot = m_MenuColumn.pivot;
        m_Panel.sizeDelta = m_MenuColumn.sizeDelta;
        m_Panel.anchoredPosition3D = m_MenuColumn.anchoredPosition3D;
        m_Panel.localRotation = m_MenuColumn.localRotation;
        m_Panel.localScale = m_MenuColumn.localScale;
        var background = panel.GetComponent<Image>();
        background.color = new Color(0.96f, 0.97f, 0.98f, 0.96f);
        background.raycastTarget = false;
        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateLabel("Settings Title", "Settings", 28f, 40f);
        CreateLabel("Invulnerability Description", "Ignore incoming damage for testing.", 16f, 44f);
        m_InvulnerabilityButton = CreateButton("Dev Invulnerability", ToggleInvulnerability, 68f);
        m_InvulnerabilityLabel = m_InvulnerabilityButton.GetComponentInChildren<TMP_Text>();
        m_InvulnerabilityLabel.fontSize = 20f;
        m_InvulnerabilityLabel.textWrappingMode = TextWrappingModes.NoWrap;
        var back = CreateButton("Back", Close, 44f);
        var backLabel = back.GetComponentInChildren<TMP_Text>();
        backLabel.text = "Back";
        backLabel.fontSize = 24f;
        RefreshInvulnerability(DevelopmentSettings.InvulnerabilityEnabled);
        panel.SetActive(false);
    }

    Button CreateButton(string name, UnityAction onClick, float height)
    {
        var button = Instantiate(m_ButtonTemplate, m_Panel, false);
        button.name = name;
        // Clear the template's persistent Play action as well as runtime listeners.
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(onClick);
        var size = button.GetComponent<LayoutElement>();
        if (size == null) size = button.gameObject.AddComponent<LayoutElement>();
        size.preferredHeight = height;
        size.flexibleHeight = 0f;
        return button;
    }

    void CreateLabel(string name, string text, float fontSize, float height)
    {
        var label = Instantiate(m_ButtonTemplate.GetComponentInChildren<TMP_Text>(), m_Panel, false);
        label.name = name;
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        var size = label.gameObject.AddComponent<LayoutElement>();
        size.preferredHeight = height;
        size.flexibleHeight = 0f;
    }
}
