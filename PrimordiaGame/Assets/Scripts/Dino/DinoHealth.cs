using UnityEngine;
using UnityEngine.AI;

public class DinoHealth : MonoBehaviour, IDamageable
{
    public DinoProfile profile;
    public bool IsDead { get; private set; }

    private float currentHealth;
    private Animator animator;
    private Transform lastAttacker;   // who dealt the most recent damage

    public float CurrentHealth => currentHealth;

    public event System.Action<CombatHit> damaged;
    public event System.Action died;

    void Start()
    {
        currentHealth = profile.health;
        if (profile.isAlpha) currentHealth *= profile.alphaHealthMult;
        animator = GetComponentInChildren<Animator>();
    }

    public void TakeDamage(float amount, Transform attacker)
    {
        ApplyHit(new CombatHit(amount, transform.position, Vector3.up, attacker));
    }

    public void ApplyHit(CombatHit hit)
    {
        if (IsDead || hit.Damage <= 0f) return;
        currentHealth -= hit.Damage;
        lastAttacker = hit.Attacker;
        damaged?.Invoke(hit);

        DinoAI ai = GetComponent<DinoAI>();
        if (ai != null && hit.Attacker != null) ai.OnAttacked(hit.Attacker);

        if (currentHealth <= 0) Die();
    }

    void Die()
    {
        IsDead = true;
        Debug.Log(name + " died!");

        // spawn loot drops
        DinoLoot loot = GetComponent<DinoLoot>();
        if (loot != null) loot.DropLoot();

        // grant XP ONLY if the player landed the killing blow
        if (lastAttacker != null && lastAttacker.CompareTag("Player"))
        {
            PlayerProgression prog = lastAttacker.GetComponent<PlayerProgression>();
            if (prog != null)
            {
                int xp = profile.xpReward;
                if (profile.isAlpha) xp = Mathf.RoundToInt(xp * profile.alphaXpMult);
                prog.AddXP(xp);
            }
        }
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

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            GameObject pgo = GameObject.FindWithTag("Player");
            TakeDamage(9999, pgo != null ? pgo.transform : null);
        }
    }
}
