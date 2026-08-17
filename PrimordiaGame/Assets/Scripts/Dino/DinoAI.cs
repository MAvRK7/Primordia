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

    private Transform currentTarget;
    private float lastAttackTime = -999f;
    private float lastSensedTime = -999f;   // refreshed by sight OR hearing OR proximity
    private float wanderTimer;
    private NavMeshAgent agent;
    private Animator animator;
    private Vector3 lastHeardPos;
    private bool hasHeardSomething;
    private Vector3 spawnPos;

    private enum State { Wander, Investigate, Chase, Attack, Flee }
    private State state = State.Wander;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        agent.avoidancePriority = Random.Range(1, 99);
        spawnPos = transform.position;

        if (player == null)
        {
            GameObject pgo = GameObject.FindWithTag("Player");
            if (pgo != null) player = pgo.transform;
        }
    }

    void Update()
    {
        // ---- Target selection ----
        bool isPredator = profile.behaviour == DinoBehaviour.PredatorHuntsPlayer
                       || profile.behaviour == DinoBehaviour.PredatorHuntsHerbivores;

        if (isPredator)
            currentTarget = ChoosePredatorTarget();
        // PassiveRetaliator & Flees: currentTarget set by OnAttacked / their own branch.

        // ---- Drop the target if it has died (any behaviour) ----
        if (currentTarget != null && IsDead(currentTarget))
        {
            currentTarget = null;
            if (state == State.Chase || state == State.Attack) state = State.Wander;
        }

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

        switch (profile.behaviour)
        {
            case DinoBehaviour.PredatorHuntsPlayer:
            case DinoBehaviour.PredatorHuntsHerbivores:
                if (leashed)
                {
                    lastSensedTime = -999f;
                    if (state == State.Chase || state == State.Attack) state = State.Wander;
                }
                else if (currentTarget != null && (recentlySensed || CanSense(currentTarget)))
                {
                    // have a target we can sense (now or recently) -> commit to combat,
                    // interrupting wander immediately
                    float d = Vector3.Distance(transform.position, currentTarget.position);
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
                    if (!hasHeardSomething) state = State.Wander;
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
                    if (state == State.Chase || state == State.Attack) state = State.Wander;
                }
                else if (currentTarget != null && (recentlySensed || CanSense(currentTarget)))
                {
                    float d = Vector3.Distance(transform.position, currentTarget.position);
                    state = ResolveCombatState(d, currentTarget);
                }
                else if (state == State.Chase || state == State.Attack)
                {
                    currentTarget = null;
                    state = State.Wander;
                }
                break;
        }

        if (agent.isOnNavMesh) Act();
        animator.SetFloat("Speed", agent.velocity.magnitude);
    }

    // ---- Auto-target: prefer player or herbivore per profile, switch when sensed ----
    Transform ChoosePredatorTarget()
    {
        Transform herb = FindNearestHerbivore();
        bool herbOk   = herb != null && !IsDead(herb) && CanSense(herb);
        bool playerOk = player != null && !IsDead(player) && CanSense(player);

        if (profile.prefersPlayer)
        {
            if (playerOk) return player;
            if (herbOk)   return herb;
        }
        else
        {
            if (herbOk)   return herb;
            if (playerOk) return player;
        }

        // keep an already-locked living target until the give-up timer lapses
        if (currentTarget != null && !IsDead(currentTarget)) return currentTarget;
        return null;
    }

    bool IsDead(Transform t)
    {
        if (t == null) return true;
        PlayerHealth php = t.GetComponent<PlayerHealth>();
        if (php != null && php.IsDead) return true;
        DinoHealth dh = t.GetComponent<DinoHealth>();
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
            return (d > combat * 1.6f) ? State.Chase : State.Attack;
        else
            return (d <= combat * 1.15f) ? State.Attack : State.Chase;
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
                if (currentTarget == null) { state = State.Wander; break; }
                agent.isStopped = false;
                agent.speed = profile.moveSpeed;
                agent.stoppingDistance = CombatDistance(currentTarget);
                agent.SetDestination(currentTarget.position);
                break;

            case State.Attack:
                if (currentTarget == null) { state = State.Wander; agent.stoppingDistance = 0f; break; }
                agent.isStopped = false;
                agent.stoppingDistance = CombatDistance(currentTarget);
                agent.SetDestination(currentTarget.position);
                FaceTarget(currentTarget);

                PlayerHealth php = currentTarget.GetComponent<PlayerHealth>();
                DinoHealth dh = currentTarget.GetComponent<DinoHealth>();
                bool targetDead = (php != null && php.IsDead) || (dh != null && dh.IsDead);
                if (targetDead)
                {
                    currentTarget = null;
                    state = State.Wander;
                    agent.stoppingDistance = 0f;
                    break;
                }
                if (Time.time - lastAttackTime >= profile.attackCooldown)
                {
                    lastAttackTime = Time.time;
                    animator.SetTrigger("Attack");
                    if (php != null) php.TakeDamage(profile.attackDamage);
                    if (dh != null) dh.TakeDamage(profile.attackDamage, transform);
                }
                break;

            case State.Flee:
                // Run from the attacker if there is one, otherwise from the player.
                Transform threat = currentTarget != null ? currentTarget : player;
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

    Transform FindNearestHerbivore()
    {
        DinoAI[] all = FindObjectsByType<DinoAI>(FindObjectsSortMode.None);
        Transform nearest = null;
        float best = Mathf.Infinity;
        foreach (DinoAI d in all)
        {
            if (d == this) continue;
            bool isHerbivore = d.profile.behaviour == DinoBehaviour.PassiveRetaliator
                            || d.profile.behaviour == DinoBehaviour.Flees;
            if (!isHerbivore) continue;
            if (IsDead(d.transform)) continue;
            float dist = Vector3.Distance(transform.position, d.transform.position);
            if (dist < best) { best = dist; nearest = d.transform; }
        }
        return nearest;
    }

    float CombatDistance(Transform target)
    {
        NavMeshAgent ta = target.GetComponent<NavMeshAgent>();
        float myR = agent != null ? agent.radius : 0f;
        float targetR = ta != null ? ta.radius : 0f;
        return profile.attackRange + myR + targetR;
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
        Vector3 to = t.position - transform.position;
        if (to.magnitude > profile.sightRange) return false;
        if (Vector3.Angle(transform.forward, to) > profile.sightAngle) return false;
        if (Physics.Raycast(transform.position, to.normalized, out RaycastHit hit, profile.sightRange))
        {
            if (hit.transform != t && !hit.transform.IsChildOf(t) && !t.IsChildOf(hit.transform))
                return false;
        }
        return true;
    }

    bool CanHearNoise()
    {
        if (Time.time - PlayerNoise.lastNoiseTime > profile.noiseMemory) return false;
        return Vector3.Distance(transform.position, PlayerNoise.lastNoisePos) < profile.hearingRadius;
    }
}