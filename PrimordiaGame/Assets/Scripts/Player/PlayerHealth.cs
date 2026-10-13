using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    public bool IsDead { get; private set; }
    public float maxHealth = 100f;
    [Tooltip("Ignore incoming damage while enabled. Controlled by Settings > Invulnerability (dev) in the main menu.")]
    public bool invulnerable = false;
    private float currentHealth;
    Camera playerCamera;

    // Follow room-scale movement as well as movement of the XR origin.
    public Transform TargetTransform
    {
        get
        {
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
            return playerCamera != null ? playerCamera.transform : transform;
        }
    }

    public static Transform ResolveTarget(Transform reference = null)
    {
        if (reference == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) return null;
            reference = player.transform;
        }
        var health = reference.GetComponentInParent<PlayerHealth>();
        return health != null ? health.TargetTransform : reference;
    }

    public float CurrentHealth => currentHealth;

    public event System.Action<CombatHit> damaged;
    public event System.Action died;
    public event System.Action restored;

    void OnEnable()
    {
        invulnerable = DevelopmentSettings.InvulnerabilityEnabled;
        DevelopmentSettings.InvulnerabilityChanged += SetInvulnerability;
    }

    void OnDisable()
    {
        DevelopmentSettings.InvulnerabilityChanged -= SetInvulnerability;
    }

    void SetInvulnerability(bool enabled) => invulnerable = enabled;

    void Start()
    {
        RestoreHealth();
    }

    public void RestoreHealth()
    {
        currentHealth = maxHealth;
        IsDead = false;
        restored?.Invoke();
    }

    public void TakeDamage(float amount)
    {
        ApplyHit(new CombatHit(amount, transform.position, Vector3.up, null));
    }

    public void ApplyHit(CombatHit hit)
    {
        if (invulnerable || IsDead || hit.Damage <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - hit.Damage);
        damaged?.Invoke(hit);
        Debug.Log("Player took " + hit.Damage + " damage. Health: " + currentHealth);

        if (currentHealth <= 0)
        {
            IsDead = true;
            Debug.Log("Player died!");
            died?.Invoke();
        }
    }
}
