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
    private Renderer[] corpseRenderers;

    public float CurrentHealth => currentHealth;
    public BoxCollider CorpseCollider { get; private set; }

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
        currentHealth -= hit.Damage;
        lastAttacker = hit.Attacker;
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
        DinoMover mover = GetComponent<DinoMover>();
        if (mover != null) mover.enabled = false;
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        if (animator != null) animator.SetTrigger("Die");
        foreach (var hitCollider in GetComponentsInChildren<Collider>())
            hitCollider.enabled = false;
        EnableCorpseTrigger();
        died?.Invoke();
        Destroy(gameObject, 5f);
    }

    void EnableCorpseTrigger()
    {
        // A kinematic body allows trigger callbacks without making the corpse
        // fall through the terrain after its solid colliders are disabled.
        var body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        foreach (var corpseBody in GetComponentsInChildren<Rigidbody>(true))
        {
            corpseBody.isKinematic = true;
            corpseBody.useGravity = false;
        }

        corpseRenderers = GetComponentsInChildren<Renderer>();
        CorpseCollider = gameObject.AddComponent<BoxCollider>();
        CorpseCollider.isTrigger = true;
        UpdateCorpseBounds();
    }

    void LateUpdate()
    {
        if (CorpseCollider != null)
            UpdateCorpseBounds();
    }

    void UpdateCorpseBounds()
    {
        // Fit in the dinosaur's local space, including scaled alpha variants,
        // and follow renderer bounds as the death animation changes the pose.
        var bounds = new Bounds(Vector3.up * 0.5f, Vector3.one);
        var hasPoint = false;
        foreach (var renderer in corpseRenderers)
        {
            if (renderer == null || !renderer.enabled ||
                (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer))
                continue;

            var worldBounds = renderer.bounds;
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                var point = transform.InverseTransformPoint(worldBounds.center +
                    Vector3.Scale(worldBounds.extents, new Vector3(x, y, z)));
                if (!hasPoint)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    hasPoint = true;
                }
                else bounds.Encapsulate(point);
            }
        }
        CorpseCollider.center = bounds.center;
        CorpseCollider.size = Vector3.Max(bounds.size, Vector3.one * 0.1f);
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
