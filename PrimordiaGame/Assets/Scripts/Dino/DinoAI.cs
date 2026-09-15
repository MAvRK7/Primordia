using UnityEngine;
using UnityEngine.AI;

public class DinoAI : MonoBehaviour
{
    public Transform player;
    public DinoProfile profile;

    [Header("Wander")]
    public float wanderRadius = 8f;
    public float wanderPause  = 3f;
    public float wanderSpeed  = 2f;

    [Header("Sensing")]
    public float proximityAware = 25f;   // anything this close is sensed regardless of FOV

    [Header("Pack (optional)")]
    public RaptorPack pack;              // assign in Inspector to make this raptor part of a pack

    private Transform currentTarget;
    private float lastSensedTime = -999f;   // refreshed by sight OR hearing OR proximity
    private float wanderTimer;
    private NavMeshAgent agent;
    private Animator animator;
    private AudioSource audioSource;        // optional — no AudioSource means silent
    private DinoAttack attack;              // attack module (auto-added if not on the prefab)
    private Vector3 lastHeardPos;
    private bool hasHeardSomething;
    private Vector3 spawnPos;

    private bool hadLiveTarget;             // did we hold a valid target last check?
    private float nextIdleSoundTime;
    private bool dormant;                   // put to sleep by DinoAIManager (far from player)

    private enum State { Wander, Investigate, Chase, Attack, Flee }
    private State state = State.Wander;
    private State prevState = State.Wander;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        audioSource = GetComponent<AudioSource>();          // optional
        agent.avoidancePriority = Random.Range(1, 99);
        spawnPos = transform.position;

        // Attack module: use the one on the prefab, or create it silently so
        // existing prefabs keep working with no editor changes.
        attack = GetComponent<DinoAttack>();
        if (attack == null) attack = gameObject.AddComponent<DinoAttack>();
        attack.Init(this, profile, animator, audioSource);

        if (player == null)
        {
            GameObject pgo = GameObject.FindWithTag("Player");
            if (pgo != null) player = pgo.transform;
        }

        if (pack != null) pack.Register(this);

        ScheduleNextIdleSound();
    }

    void OnEnable()
    {
        if (DinoAIManager.Instance != null) DinoAIManager.Instance.Register(this);
    }

    void OnDisable()
    {
        if (DinoAIManager.Instance != null) DinoAIManager.Instance.Unregister(this);
    }

    void Update()
    {
        if (profile == null || agent == null) return;

        // ---- (1) TARGET VALIDITY: runs EVERY frame, before anything else. ----
        // A Destroy()ed corpse reads as fake-null, so "null" and "dead" must be
        // handled by the same branch or the dino can be left frozen in Attack.
        ValidateTarget();

        // ---- (3) Performance manager (optional — null manager = old behaviour) ----
        DinoAIManager mgr = DinoAIManager.Instance;
        if (mgr != null && !mgr.IsActive(this))
        {
            GoDormant();
            return;
        }
        if (dormant) WakeUp();

        // Heavy sensing / target selection is time-sliced across frames by the
        // manager. Movement (Act) still runs every frame so motion stays smooth.
        bool maySense = mgr == null || mgr.MaySense(this);
        if (maySense) Think();

        if (agent.isOnNavMesh) Act();

        // ---- (2) Sound: chase stinger on transition, idle calls while wandering ----
        if (state != prevState) OnStateChanged(prevState, state);
        prevState = state;
        TickIdleSound();

        if (animator != null) animator.SetFloat("Speed", agent.velocity.magnitude);
    }

    // ==================== (1) DEAD / DESTROYED TARGET HANDLING ====================

    // True when the target is gone for any reason: never assigned, Destroy()ed
    // (fake-null), or alive-but-dead. One predicate, used everywhere.
    bool TargetGone(Transform t)
    {
        return t == null || IsDead(t);
    }

    // Clears a gone target and guarantees we cannot sit frozen pointing at a corpse.
    void ValidateTarget()
    {
        if (!TargetGone(currentTarget))
        {
            hadLiveTarget = true;
            return;
        }

        // Only stand the pack down if WE were actually holding that target —
        // otherwise a target-less member would wipe the pack's shared prey.
        if (hadLiveTarget && pack != null) pack.ClearTarget();

        currentTarget = null;
        hadLiveTarget = false;

        if (state == State.Chase || state == State.Attack) ReturnToWander();
    }

    // Hard reset out of combat: state, stopping distance, stuck attack pose.
    void ReturnToWander()
    {
        state = State.Wander;
        wanderTimer = wanderPause;              // pick a new wander point promptly
        if (agent != null)
        {
            agent.stoppingDistance = 0f;
            if (agent.isOnNavMesh) agent.isStopped = false;
        }
        if (animator != null) animator.ResetTrigger("Attack");
    }

    // ==================== SENSING + STATE MACHINE ====================

    void Think()
    {
        // ---- Target selection ----
        bool isPredator = profile.behaviour == DinoBehaviour.PredatorHuntsPlayer
                       || profile.behaviour == DinoBehaviour.PredatorHuntsHerbivores;

        if (isPredator)
            currentTarget = ChoosePredatorTarget();
        // PassiveRetaliator & Flees: currentTarget set by OnAttacked / their own branch.

        // Re-validate: selection can hand back a target that died this same frame.
        ValidateTarget();

        // ---- Sense refresh: sight OR hearing OR close proximity keeps engagement alive ----
        bool sees  = currentTarget != null && CanSee(currentTarget);
        bool hears = currentTarget == player && CanHearNoise();
        bool near  = currentTarget != null &&
                     Vector3.Distance(transform.position, currentTarget.position) < proximityAware;
        if (sees || hears || near) lastSensedTime = Time.time;
        bool recentlySensed = Time.time - lastSensedTime < profile.giveUpTimer;

        // ---- Leash: too far from spawn = give up regardless ----
        float distFromSpawn = Vector3.Distance(transform.position, spawnPos);
        bool leashed = distFromSpawn > profile.leashRange;

        // pack members commit to the shared target even without personally sensing it
        bool packTarget = pack != null && pack.SharedTarget == currentTarget && currentTarget != null;

        switch (profile.behaviour)
        {
            case DinoBehaviour.PredatorHuntsPlayer:
            case DinoBehaviour.PredatorHuntsHerbivores:
                if (leashed)
                {
                    lastSensedTime = -999f;
                    if (state == State.Chase || state == State.Attack) ReturnToWander();
                }
                else if (currentTarget != null && (recentlySensed || CanSense(currentTarget) || packTarget))
                {
                    float d = PlanarDistance(transform.position, currentTarget.position);
                    state = ResolveCombatState(d, currentTarget);
                }
                else if (hears && currentTarget == player)
                {
                    lastHeardPos = PlayerNoise.lastNoisePos;
                    hasHeardSomething = true;
                    state = State.Investigate;
                }
                else if (state == State.Chase || state == State.Attack)
                {
                    if (!hasHeardSomething) ReturnToWander();
                }
                break;

            case DinoBehaviour.Flees:
                // Flee from the player if sensed, OR from whatever attacked us (currentTarget).
                bool threatened = (player != null && !IsDead(player) && (CanSee(player) || CanHearNoise()))
                               || (currentTarget != null && !IsDead(currentTarget) && recentlySensed);
                if (threatened) state = State.Flee;
                else if (state == State.Flee) { currentTarget = null; state = State.Wander; }
                break;

            case DinoBehaviour.PassiveRetaliator:
                if (leashed)
                {
                    lastSensedTime = -999f;
                    currentTarget = null;
                    if (state == State.Chase || state == State.Attack) ReturnToWander();
                }
                else if (currentTarget != null && (recentlySensed || CanSense(currentTarget)))
                {
                    float d = PlanarDistance(transform.position, currentTarget.position);
                    state = ResolveCombatState(d, currentTarget);
                }
                else if (state == State.Chase || state == State.Attack)
                {
                    currentTarget = null;
                    ReturnToWander();
                }
                break;
        }
    }

    // ---- Auto-target: pack-shared if in a pack, else prefer player/herbivore per profile ----
    Transform ChoosePredatorTarget()
    {
        // If in a pack and the pack already has a live target, hunt that (shared targeting).
        if (pack != null && !TargetGone(pack.SharedTarget))
            return pack.SharedTarget;

        Transform herb = FindNearestHerbivore();
        bool herbOk   = !TargetGone(herb) && CanSense(herb);
        bool playerOk = !TargetGone(player) && CanSense(player);

        Transform chosen = null;
        if (profile.prefersPlayer)
        {
            if (playerOk) chosen = player;
            else if (herbOk) chosen = herb;
        }
        else
        {
            if (herbOk) chosen = herb;
            else if (playerOk) chosen = player;
        }

        // If I found prey and I'm in a pack, tell the pack so everyone converges.
        if (chosen != null && pack != null)
            pack.ReportTarget(chosen);

        if (chosen != null) return chosen;

        // keep an already-locked living target until the give-up timer lapses
        if (!TargetGone(currentTarget)) return currentTarget;
        return null;
    }

    bool IsDead(Transform t)
    {
        if (t == null) return true;
        PlayerHealth php = t.GetComponentInParent<PlayerHealth>();
        if (php != null && php.IsDead) return true;
        DinoHealth dh = t.GetComponentInParent<DinoHealth>();
        if (dh != null && dh.IsDead) return true;
        return false;
    }

    // ---- Can this dino currently see OR hear OR feel (proximity) the target? ----
    bool CanSense(Transform t)
    {
        if (t == null) return false;
        if (Vector3.Distance(transform.position, t.position) < proximityAware) return true;
        if (CanSee(t)) return true;
        if (t == player && CanHearNoise()) return true;
        return false;
    }

    // Hysteresis: enter Attack at combat range, don't drop to Chase until clearly out
    State ResolveCombatState(float d, Transform target)
    {
        float combat = CombatDistance(target);
        if (state == State.Attack)
            return (d > combat * 1.25f) ? State.Chase : State.Attack;
        else
            return (d <= combat) ? State.Attack : State.Chase;
    }

    void Act()
    {
        switch (state)
        {
            case State.Wander:
                agent.isStopped = false;
                agent.stoppingDistance = 0f;
                agent.speed = wanderSpeed;
                if (!agent.pathPending && agent.remainingDistance < 1f)
                {
                    wanderTimer += Time.deltaTime;
                    if (wanderTimer >= wanderPause)
                    {
                        agent.SetDestination(RandomWanderPoint());
                        wanderTimer = 0f;
                    }
                }
                break;

            case State.Investigate:
                agent.isStopped = false;
                agent.stoppingDistance = 0f;
                agent.speed = profile.moveSpeed;
                agent.SetDestination(lastHeardPos);
                if (!agent.pathPending && agent.remainingDistance < 1.5f)
                {
                    hasHeardSomething = false;
                    state = State.Wander;
                }
                break;

            case State.Chase:
                // (1) Guard: dead or destroyed target can never hold us in Chase.
                if (TargetGone(currentTarget)) { currentTarget = null; ReturnToWander(); break; }
                agent.isStopped = false;
                agent.speed = profile.moveSpeed;
                agent.stoppingDistance = CombatDistance(currentTarget);
                agent.SetDestination(currentTarget.position);
                break;

            case State.Attack:
                if (TargetGone(currentTarget)) { currentTarget = null; ReturnToWander(); break; }
                // Stop locomotion while attacking. Continuing to drive toward a headset target
                // lets an agent push its centre through the player's CharacterController.
                agent.isStopped = true;
                agent.stoppingDistance = CombatDistance(currentTarget);
                FaceTarget(currentTarget);

                // (4) Attack execution lives in DinoAttack now — same cooldown,
                // same damage, same animator trigger.
                if (attack != null) attack.TryAttack(currentTarget);
                break;

            case State.Flee:
                // Run from the attacker if there is one, otherwise from the player.
                Transform threat = !TargetGone(currentTarget) ? currentTarget : player;
                if (threat == null) { state = State.Wander; break; }
                agent.isStopped = false;
                agent.stoppingDistance = 0f;
                agent.speed = profile.moveSpeed;
                Vector3 away = (transform.position - threat.position).normalized;
                agent.SetDestination(transform.position + away * 8f);
                break;
        }
    }

    public void OnAttacked(Transform attacker)
    {
        if (profile.behaviour == DinoBehaviour.PassiveRetaliator || profile.behaviour == DinoBehaviour.Flees)
        {
            currentTarget = attacker;
            lastSensedTime = Time.time;   // being hit counts as sensing
        }
    }

    // ==================== (2) SOUND ====================

    // Plays a clip if we have both an AudioSource and a clip. Silent otherwise — never errors.
    public void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }

    void OnStateChanged(State from, State to)
    {
        // chase stinger fires once, on the transition into Chase
        if (to == State.Chase && from != State.Chase)
            PlayClip(profile.chaseSound);
    }

    void TickIdleSound()
    {
        if (state != State.Wander) return;
        if (Time.time < nextIdleSoundTime) return;
        PlayClip(profile.idleSound);
        ScheduleNextIdleSound();
    }

    void ScheduleNextIdleSound()
    {
        float baseInterval = (profile != null && profile.idleSoundInterval > 0f)
            ? profile.idleSoundInterval : 8f;
        nextIdleSoundTime = Time.time + baseInterval * Random.Range(0.6f, 1.4f);
    }

    // ==================== (3) MANAGER SLEEP / WAKE ====================

    void GoDormant()
    {
        if (dormant) return;
        dormant = true;
        currentTarget = null;
        hadLiveTarget = false;
        state = State.Wander;
        prevState = State.Wander;
        if (agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }
        if (animator != null) animator.SetFloat("Speed", 0f);
    }

    void WakeUp()
    {
        dormant = false;
        if (agent.isOnNavMesh) agent.isStopped = false;
        lastSensedTime = -999f;     // re-sense from scratch
        wanderTimer = wanderPause;
        ScheduleNextIdleSound();
    }

    // ==================== HELPERS ====================

    Transform FindNearestHerbivore()
    {
        DinoAI[] all = FindObjectsByType<DinoAI>(FindObjectsSortMode.None);
        Transform nearest = null;
        float best = Mathf.Infinity;
        foreach (DinoAI d in all)
        {
            if (d == this) continue;

            // guard against a missing profile (could be null after a merge/reimport)
            if (d.profile == null)
            {
                Debug.LogWarning($"{d.name} has NO profile assigned!");
                continue;
            }

            bool isHerbivore = d.profile.behaviour == DinoBehaviour.PassiveRetaliator
                            || d.profile.behaviour == DinoBehaviour.Flees;
            if (!isHerbivore) continue;
            if (IsDead(d.transform)) continue;

            float dist = Vector3.Distance(transform.position, d.transform.position);
            if (dist < best) { best = dist; nearest = d.transform; }
        }
        return nearest;
    }

    public float CombatDistance(Transform target)
    {
        if (target == null) return profile.attackRange;
        NavMeshAgent ta = target.GetComponentInParent<NavMeshAgent>();
        CharacterController targetController = target.GetComponentInParent<CharacterController>();
        float myR = agent != null ? agent.radius : 0f;
        float targetR = ta != null
            ? ta.radius
            : targetController != null ? targetController.radius : 0f;
        return profile.attackRange + myR + targetR;
    }

    static float PlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    Vector3 RandomWanderPoint()
    {
        Vector3 random = Random.insideUnitSphere * wanderRadius + transform.position;
        if (NavMesh.SamplePosition(random, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            return hit.position;
        return transform.position;
    }

    void FaceTarget(Transform t)
    {
        Vector3 dir = t.position - transform.position;
        dir.y = 0;
        if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);
    }

    bool CanSee(Transform t)
    {
        if (t == null) return false;
        Vector3 to = t.position - transform.position;
        if (to.magnitude > profile.sightRange) return false;
        if (Vector3.Angle(transform.forward, to) > profile.sightAngle) return false;
        if (Physics.Raycast(transform.position, to.normalized, out RaycastHit hit, profile.sightRange))
        {
            bool hitTargetHierarchy = hit.transform == t ||
                hit.transform.IsChildOf(t) ||
                t.IsChildOf(hit.transform);
            if (!hitTargetHierarchy) return false;
        }
        return true;
    }

    bool CanHearNoise()
    {
        if (Time.time - PlayerNoise.lastNoiseTime > profile.noiseMemory) return false;
        float audibleRange = Mathf.Min(profile.hearingRadius, PlayerNoise.lastNoiseRange);
        return audibleRange > 0f &&
            Vector3.Distance(transform.position, PlayerNoise.lastNoisePos) < audibleRange;
    }
}
