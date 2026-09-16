using UnityEngine;
using UnityEngine.AI;

public class DinoHealth : MonoBehaviour
{
    public DinoProfile profile;
    public bool IsDead { get; private set; }

    private float currentHealth;
    private Animator animator;
    private AudioSource audioSource;   // optional — no AudioSource means silent
    private Transform lastAttacker;    // who dealt the most recent damage

    void Start()
    {
        currentHealth = profile.health;
        if (profile.isAlpha) currentHealth *= profile.alphaHealthMult;
        animator = GetComponentInChildren<Animator>();
        audioSource = GetComponent<AudioSource>();   // optional
    }

    public void TakeDamage(float amount, Transform attacker)
    {
        if (IsDead) return;
        currentHealth -= amount;
        lastAttacker = attacker;   // remember who hit us

        DinoAI ai = GetComponent<DinoAI>();
        if (ai != null && attacker != null) ai.OnAttacked(attacker);

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
        bool playerKill = lastAttacker != null && lastAttacker.CompareTag("Player");

        // grant XP ONLY if the player landed the killing blow — this happens on
        // DEATH, not on butcher. Butchering is just a loot-release action.
        if (playerKill)
        {
            PlayerProgression prog = lastAttacker.GetComponent<PlayerProgression>();
            if (prog != null)
            {
                int xp = profile.xpReward;
                if (profile.isAlpha) xp = Mathf.RoundToInt(xp * profile.alphaXpMult);
                prog.AddXP(xp);
            }
        }

        // Stop the AI and freeze the agent in place — the corpse persists in the
        // world until Butcher() is called (VR interactor will call it later).
        DinoAI ai = GetComponent<DinoAI>();
        if (ai != null) ai.enabled = false;
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        if (animator != null) animator.SetTrigger("Die");

        // ---- Does this body stick around as a butcherable corpse? ----
        // Player kill  -> always (that's the player's kill, they earned the loot).
        // Dino kill / unknown killer -> only profile.dinoKillCorpseChance of the
        // time, so predator-vs-herbivore fights don't litter the map with
        // lootable carcasses the player never earned.
        bool spawnCorpse = playerKill || Random.value < profile.dinoKillCorpseChance;

        if (spawnCorpse)
        {
            // Attach the corpse component in place of the old auto-Destroy(). It
            // holds the profile + the DinoLoot ref so Butcher() can release loot.
            // No Destroy() here — the corpse stays until butchered; DinoCorpse
            // handles the final removal (after Butcher() + a short delay).
            DinoCorpse corpse = GetComponent<DinoCorpse>();
            if (corpse == null) corpse = gameObject.AddComponent<DinoCorpse>();
            corpse.Init(profile, GetComponent<DinoLoot>(), animator);
        }
        else
        {
            // No corpse, no loot: let the death animation read, then remove the
            // body. No DinoCorpse is added, so nothing can ever butcher it.
            float delay = Mathf.Max(0f, profile.noCorpseDespawnDelay);
            Destroy(gameObject, delay);
        }
    }

    // Plays a clip if we have both an AudioSource and a clip. Silent otherwise — never errors.
    void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
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
