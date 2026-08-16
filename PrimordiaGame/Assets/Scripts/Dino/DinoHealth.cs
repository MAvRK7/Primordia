using UnityEngine;
using UnityEngine.AI;

public class DinoHealth : MonoBehaviour, IDamageable
{
    public DinoProfile profile;
    public bool IsDead { get; private set; }

    private float currentHealth;
    private Animator animator;

    public float CurrentHealth => currentHealth;

    public event System.Action<CombatHit> damaged;
    public event System.Action died;

    void Start()
    {
        currentHealth = profile.health;
        animator = GetComponentInChildren<Animator>();
    }

    // attacker = who dealt the damage (player or another dino); can be null
    public void TakeDamage(float amount, Transform attacker)
    {
        ApplyHit(new CombatHit(amount, transform.position, Vector3.up, attacker));
    }

    public void ApplyHit(CombatHit hit)
    {
        if (IsDead || hit.Damage <= 0f) return;
        currentHealth -= hit.Damage;
        damaged?.Invoke(hit);

        // tell my own AI I was hit (for passive retaliators)
        DinoAI ai = GetComponent<DinoAI>();
        if (ai != null && hit.Attacker != null) ai.OnAttacked(hit.Attacker);

        if (currentHealth <= 0) Die();
    }

    void Die()
    {
        IsDead = true;
        Debug.Log(name + " died!");
        DinoAI ai = GetComponent<DinoAI>();
        if (ai != null) ai.enabled = false;
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        if (animator != null) animator.SetTrigger("Die");
        foreach (var hitCollider in GetComponentsInChildren<Collider>())
            hitCollider.enabled = false;
        died?.Invoke();
        Destroy(gameObject, 5f);
    }
}
