using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    public bool IsDead { get; private set; }
    public float maxHealth = 100f;
    private float currentHealth;

    public float CurrentHealth => currentHealth;

    public event System.Action<CombatHit> damaged;
    public event System.Action died;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        ApplyHit(new CombatHit(amount, transform.position, Vector3.up, null));
    }

    public void ApplyHit(CombatHit hit)
    {
        if (IsDead || hit.Damage <= 0f)
            return;

        currentHealth -= hit.Damage;
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
