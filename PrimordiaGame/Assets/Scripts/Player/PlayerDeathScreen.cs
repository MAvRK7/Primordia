using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>Keeps respawning and XR UI available while death suspends locomotion.</summary>
[DisallowMultipleComponent]
public sealed class PlayerDeathScreen : MonoBehaviour
{
    [SerializeField] PlayerHealth m_Health;
    [SerializeField] PlayerRigController m_Rig;
    [SerializeField] Camera m_ViewCamera;

    Canvas m_Canvas;
    LocomotionProvider[] m_Providers;
    bool[] m_ProviderWasEnabled;
    NearFarInteractor[] m_Interactors;
    bool[] m_BlockedUIOnSelection;
    bool m_Showing;

    void Awake()
    {
        if (m_Health == null || m_Rig == null || m_ViewCamera == null)
        {
            Debug.LogError("PlayerDeathScreen needs player health, the rig controller and the XR camera.", this);
            enabled = false;
        }
    }

    void OnEnable()
    {
        if (m_Health == null) return;
        m_Health.died += Show;
        m_Health.restored += Hide;
        if (m_Health.IsDead) Show();
    }

    void OnDisable()
    {
        if (m_Health != null)
        {
            m_Health.died -= Show;
            m_Health.restored -= Hide;
        }
        Hide();
    }

    void OnDestroy()
    {
        if (m_Canvas != null) Destroy(m_Canvas.gameObject);
    }

    void Show()
    {
        if (m_Showing) return;
        if (m_Canvas == null) CreateDisplay();
        m_Showing = true;

        // Leave tracked poses, hands, UI interactors and the respawn action running.
        m_Providers = m_Health.GetComponentsInChildren<LocomotionProvider>(true);
        m_ProviderWasEnabled = new bool[m_Providers.Length];
        for (int i = 0; i < m_Providers.Length; i++)
        {
            m_ProviderWasEnabled[i] = m_Providers[i].enabled;
            m_Providers[i].enabled = false;
        }
        // The player may die with a weapon in each hand. Let those same pointing
        // rays use the respawn button without requiring the weapons to be dropped.
        m_Interactors = m_Health.GetComponentsInChildren<NearFarInteractor>(true);
        m_BlockedUIOnSelection = new bool[m_Interactors.Length];
        for (int i = 0; i < m_Interactors.Length; i++)
        {
            m_BlockedUIOnSelection[i] = m_Interactors[i].blockUIOnInteractableSelection;
            m_Interactors[i].blockUIOnInteractableSelection = false;
        }
        m_Canvas.gameObject.SetActive(true);
    }

    void Hide()
    {
        if (m_Canvas != null) m_Canvas.gameObject.SetActive(false);
        if (!m_Showing) return;
        m_Showing = false;
        for (int i = 0; i < m_Providers.Length; i++)
            if (m_Providers[i] != null) m_Providers[i].enabled = m_ProviderWasEnabled[i];
        for (int i = 0; i < m_Interactors.Length; i++)
            if (m_Interactors[i] != null)
                m_Interactors[i].blockUIOnInteractableSelection = m_BlockedUIOnSelection[i];
        m_Providers = null;
        m_ProviderWasEnabled = null;
        m_Interactors = null;
        m_BlockedUIOnSelection = null;
    }

    void CreateDisplay()
    {
        var panel = new GameObject("Death Screen", typeof(RectTransform), typeof(Canvas),
            typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
        panel.layer = 5; // UI
        panel.transform.SetParent(m_ViewCamera.transform, false);
        panel.transform.localPosition = new Vector3(0f, 0f, 1.5f);
        panel.transform.localScale = Vector3.one * 0.002f;
        var rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(620f, 340f);
        m_Canvas = panel.GetComponent<Canvas>();
        m_Canvas.renderMode = RenderMode.WorldSpace;
        m_Canvas.worldCamera = m_ViewCamera;
        m_Canvas.sortingOrder = 100;

        CreateImage("Background", rect, rect.sizeDelta, Vector2.zero,
            new Color(0.035f, 0.046f, 0.06f, 0.97f));
        CreateText("Title", rect, new Vector2(560f, 72f), new Vector2(0f, 95f),
            "YOU DIED", 46f, new Color(0.98f, 0.3f, 0.3f));
        CreateText("Description", rect, new Vector2(560f, 40f), new Vector2(0f, 35f),
            "Return to camp to recover your health.", 24f, Color.white);

        var image = CreateImage("Return to camp", rect, new Vector2(360f, 64f),
            new Vector2(0f, -35f), new Color(0.22f, 0.55f, 0.34f));
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(m_Rig.ResetToSpawn);
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        CreateText("Button Label", image.rectTransform, image.rectTransform.sizeDelta,
            Vector2.zero, "Return to camp", 28f, Color.white);
        var hint = "Press Y on the left controller";
        if (Application.isEditor) hint += " or Home on the keyboard";
        CreateText("Shortcut", rect, new Vector2(590f, 40f), new Vector2(0f, -105f),
            hint, 20f, new Color(0.75f, 0.82f, 0.88f));
    }

    static Image CreateImage(string name, RectTransform parent, Vector2 size, Vector2 position, Color color)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(Image));
        item.layer = 5;
        item.transform.SetParent(parent, false);
        var image = item.GetComponent<Image>();
        image.rectTransform.sizeDelta = size;
        image.rectTransform.anchoredPosition = position;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static void CreateText(string name, RectTransform parent, Vector2 size, Vector2 position,
        string text, float fontSize, Color color)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        item.layer = 5;
        item.transform.SetParent(parent, false);
        var label = item.GetComponent<TextMeshProUGUI>();
        label.rectTransform.sizeDelta = size;
        label.rectTransform.anchoredPosition = position;
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.text = text;
        label.color = color;
        label.raycastTarget = false;
    }
}
