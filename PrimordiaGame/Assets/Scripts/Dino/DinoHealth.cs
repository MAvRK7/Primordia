using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

public class DinoHealth : MonoBehaviour, IDamageable
{
    public DinoProfile profile;
    public bool IsDead { get; private set; }

    private float currentHealth;
    private Animator animator;
    private AudioSource audioSource;   // optional — no AudioSource means silent
    private Transform lastAttacker;    // who dealt the most recent damage

    public float CurrentHealth => currentHealth;

    public event System.Action<CombatHit> damaged;
    public event System.Action died;

    void Start()
    {
        currentHealth = profile.health;
        if (profile.isAlpha) currentHealth *= profile.alphaHealthMult;
        animator = GetComponentInChildren<Animator>();
        audioSource = GetComponent<AudioSource>();   // optional
    }

    public void TakeDamage(float amount, Transform attacker)
    {
        ApplyHit(new CombatHit(amount, transform.position, Vector3.up, attacker));
    }

    public void ApplyHit(CombatHit hit)
    {
        if (IsDead || hit.Damage <= 0f) return;
        var damageDealt = Mathf.Min(hit.Damage, Mathf.Max(0f, currentHealth));
        currentHealth -= hit.Damage;
        lastAttacker = hit.Attacker;
        CombatImpactFeedback.EnemyHit(hit, damageDealt, currentHealth <= 0f);
        damaged?.Invoke(hit);

        DinoAI ai = GetComponent<DinoAI>();
        if (ai != null && hit.Attacker != null) ai.OnAttacked(hit.Attacker);

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // hurt sound only for non-lethal hits (death has its own clip)
            PlayClip(profile.hurtSound);
        }
    }

    void Die()
    {
        IsDead = true;
        Debug.Log(name + " died!");

        PlayClip(profile.deathSound);

        // Who killed us? A destroyed attacker reads as null, so this also covers
        // the debug K-kill with no player in the scene: not a player kill.
        var killer = lastAttacker != null ? lastAttacker.GetComponentInParent<PlayerHealth>() : null;
        bool playerKill = killer != null;

        // grant XP ONLY if the player landed the killing blow — this happens on
        // DEATH, not on butcher. Butchering is just a loot-release action.
        if (playerKill)
        {
            PlayerProgression prog = killer.GetComponent<PlayerProgression>();
            if (prog != null)
            {
                int xp = profile.xpReward;
                if (profile.isAlpha) xp = Mathf.RoundToInt(xp * profile.alphaXpMult);
                prog.AddXP(xp);
            }
        }

        // Stop the AI and freeze the agent in place — the corpse persists in the
        // world until harvested or its ten-minute lifetime ends.
        DinoAI ai = GetComponent<DinoAI>();
        if (ai != null) ai.enabled = false;
        DinoMover mover = GetComponent<DinoMover>();
        if (mover != null) mover.enabled = false;
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        if (animator != null)
        {
            animator.enabled = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.ResetTrigger("Attack");
            animator.SetTrigger("Die");
        }
        foreach (var hitCollider in GetComponentsInChildren<Collider>())
            hitCollider.enabled = false;

        // Every kill leaves a body. Loot and removal happen only after butchering.
        DinoCorpse corpse = GetComponent<DinoCorpse>();
        if (corpse == null) corpse = gameObject.AddComponent<DinoCorpse>();
        corpse.Init(profile, GetComponent<DinoLoot>(), animator);
        died?.Invoke();
    }

    // Plays a clip if we have both an AudioSource and a clip. Silent otherwise — never errors.
    void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
        {
            GameObject pgo = GameObject.FindWithTag("Player");
            TakeDamage(9999, pgo != null ? pgo.transform : null);
        }
    }
}
