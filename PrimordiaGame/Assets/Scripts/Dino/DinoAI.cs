using UnityEngine;
using UnityEngine.AI;

public class DinoAI : MonoBehaviour
{
    public Transform target;
    public DinoProfile profile;   // drag a Raptor/TRex/etc asset here

    [Header("Wander")]
    public float wanderRadius = 12f;    // how far it roams from its current spot
    public float wanderPause  = 3f;     // seconds it waits between roams
    public float wanderSpeed  = 2f;     // slow walk speed while roaming

    private float lastAttackTime;
    private float wanderTimer;
    private NavMeshAgent agent;
    private Animator animator;
    private Vector3 lastHeardPos;
    private bool hasHeardSomething;

    private enum State { Idle, Wander, Investigate, Chase, Attack, Flee }
    private State state = State.Wander;   // start off roaming, not frozen

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        float distToPlayer = Vector3.Distance(transform.position, target.position);

        // --- SENSE / DECIDE ---
        bool sees  = CanSeePlayer();
        bool hears = CanHearNoise();

        if (sees)
        {
            if (profile.isPredator)
            {
                if (distToPlayer <= profile.attackRange) state = State.Attack;
                else                                     state = State.Chase;
            }
            else
            {
                state = State.Flee;
            }
        }
        else if (hears)
        {
            lastHeardPos = PlayerNoise.lastNoisePos;
            hasHeardSomething = true;
            state = profile.isPredator ? State.Investigate : State.Flee;
        }
        else
        {
            // nothing sensed → if we were hunting/fleeing, calm down to wandering
            if (state == State.Chase || state == State.Attack || state == State.Flee)
            {
                if (!hasHeardSomething) state = State.Wander;
            }
        }

        // --- ACT ---
        if (agent.isOnNavMesh)
        {
            switch (state)
            {
                case State.Idle:
                    if (agent.hasPath) agent.ResetPath();
                    break;

                case State.Wander:
                    agent.speed = wanderSpeed;                  // slow walk
                    wanderTimer += Time.deltaTime;
                    // reached the spot (or first time) and paused long enough → pick a new spot
                    if (!agent.pathPending && agent.remainingDistance < 1f)
                    {
                        if (wanderTimer >= wanderPause)
                        {
                            Vector3 next = RandomWanderPoint();
                            agent.SetDestination(next);
                            wanderTimer = 0f;
                        }
                    }
                    break;

                case State.Investigate:
                    agent.speed = profile.moveSpeed;
                    agent.SetDestination(lastHeardPos);
                    if (!agent.pathPending && agent.remainingDistance < 1.5f)
                    {
                        hasHeardSomething = false;
                        state = State.Wander;
                    }
                    break;

                case State.Chase:
                    agent.speed = profile.moveSpeed;            // fast run
                    agent.SetDestination(target.position);
                    break;

                case State.Attack:
                    agent.ResetPath();
                    FacePlayer();
                    if (Time.time - lastAttackTime >= profile.attackCooldown)
                    {
                        lastAttackTime = Time.time;
                        animator.SetTrigger("Attack");
                        PlayerHealth hp = target.GetComponent<PlayerHealth>();
                        if (hp != null) hp.TakeDamage(profile.attackDamage);
                    }
                    break;

                case State.Flee:
                    agent.speed = profile.moveSpeed;
                    Vector3 away = (transform.position - target.position).normalized;
                    Vector3 fleeTarget = transform.position + away * 8f;
                    agent.SetDestination(fleeTarget);
                    break;
            }
        }

        // --- ANIMATE ---
        animator.SetFloat("Speed", agent.velocity.magnitude);
    }

    // pick a random reachable point near the dino to roam to
    Vector3 RandomWanderPoint()
    {
        Vector3 random = Random.insideUnitSphere * wanderRadius + transform.position;
        if (NavMesh.SamplePosition(random, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            return hit.position;
        return transform.position;   // fallback: stay put if no valid point
    }

    void FacePlayer()
    {
        Vector3 dir = target.position - transform.position;
        dir.y = 0;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    bool CanSeePlayer()
    {
        Vector3 toPlayer = target.position - transform.position;
        if (toPlayer.magnitude > profile.sightRange) return false;
        if (Vector3.Angle(transform.forward, toPlayer) > profile.sightAngle) return false;
        if (Physics.Raycast(transform.position, toPlayer.normalized, out RaycastHit hit, profile.sightRange))
            if (hit.transform != target) return false;
        return true;
    }

    bool CanHearNoise()
    {
        if (Time.time - PlayerNoise.lastNoiseTime > profile.noiseMemory) return false;
        return Vector3.Distance(transform.position, PlayerNoise.lastNoisePos) < profile.hearingRadius;
    }
}