using UnityEngine;
using UnityEngine.AI;

public class DinoHealth : MonoBehaviour
{
    public DinoProfile profile;
    public bool IsDead { get; private set; }

    private float currentHealth;
    private Animator animator;

    void Start()
    {
        currentHealth = profile.health;
        animator = GetComponentInChildren<Animator>();
    }

    // attacker = who dealt the damage (player or another dino); can be null
    public void TakeDamage(float amount, Transform attacker)
    {
        if (IsDead) return;
        currentHealth -= amount;

        // tell my own AI I was hit (for passive retaliators)
        DinoAI ai = GetComponent<DinoAI>();
        if (ai != null && attacker != null) ai.OnAttacked(attacker);

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
        Destroy(gameObject, 5f);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.K)) TakeDamage(9999, null);  // TEMP test kill
    }
}