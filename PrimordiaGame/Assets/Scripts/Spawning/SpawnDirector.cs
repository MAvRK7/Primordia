using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class SpawnDirector : MonoBehaviour
{
    [Header("Biome")]
    public BiomeDef biomeDef;

    [Header("References")]
    [Tooltip("Falls back to Camera.main's transform if left unassigned.")]
    public Transform player;
    [Tooltip("Falls back to Camera.main if left unassigned.")]
    public Camera viewCamera;
    [Tooltip("The campfire safe-zone transform (e.g. the 'campfire Site' object in the scene).")]
    public Transform campfireSite;

    [Header("Caps")]
    [Tooltip("Absolute ceiling across all biomes. Kept as a separate field from BiomeDef.cap so " +
             "adding more biomes later doesn't require restructuring, even though today there's " +
             "only one biome and the two values happen to match.")]
    public int globalHardCap = 12;
    [Tooltip("biomeDef.cap is multiplied by this at night.")]
    public float nightCapMultiplier = 1.3f;
    [Tooltip("No day/night system exists yet - toggle manually for testing until one is wired in.")]
    public bool isNight = false;

    [Header("Spawn Placement (annulus around player)")]
    public float spawnRadiusMin = 35f;
    public float spawnRadiusMax = 80f;
    [Tooltip("Attempts to find a NavMesh point per spawn candidate before giving up on that candidate.")]
    public int navMeshSampleAttempts = 5;
    public float navMeshSampleMaxDistance = 5f;
    [Tooltip("Attempts to find a candidate point outside the camera's view frustum before spawning anyway.")]
    public int frustumAvoidAttempts = 5;

    [Header("Campfire Safety")]
    [Tooltip("Spawns are rejected within this distance of campfireSite. T-Rex is exempt from this check.")]
    public float campfireSafeRadius = 15f;

    [Header("Despawn")]
    public float despawnDistance = 110f;
    public float despawnLingerSeconds = 10f;

    [Header("Respawn Timer")]
    [Tooltip("Randomised delay range for each empty slot's independent respawn timer. Each slot " +
             "rolls its own timer the moment it goes empty, so multiple slots can spawn in the same " +
             "frame or close succession if their timers happen to line up - that's expected.")]
    public float respawnDelayMin = 45f;
    public float respawnDelayMax = 90f;

    private readonly List<GameObject> activeInstances = new List<GameObject>();
    private readonly Dictionary<GameObject, float> outOfRangeSince = new Dictionary<GameObject, float>();

    // One scheduled spawn time per currently-empty slot. A slot's timer is rolled fresh the
    // moment it becomes empty (startup, or a despawn freeing a slot) and fires independently
    // of every other slot - multiple slots can spawn in the same frame.
    private readonly List<float> emptySlotTimers = new List<float>();

    void Update()
    {
        if (biomeDef == null) return;

        Transform playerT = ResolvePlayer();
        if (playerT == null) return;

        int effectiveCap = ComputeEffectiveCap();

        UpdateDespawns(playerT);
        ReconcileSlotTimers(effectiveCap);
        UpdateSpawns(playerT, effectiveCap);
    }

    int ComputeEffectiveCap()
    {
        float biomeCap = biomeDef.cap * (isNight ? nightCapMultiplier : 1f);
        return Mathf.Min(globalHardCap, Mathf.FloorToInt(biomeCap));
    }

    // ---------------- Despawn ----------------

    void UpdateDespawns(Transform playerT)
    {
        for (int i = activeInstances.Count - 1; i >= 0; i--)
        {
            GameObject inst = activeInstances[i];
            if (inst == null)
            {
                activeInstances.RemoveAt(i);
                continue;
            }

            float dist = Vector3.Distance(inst.transform.position, playerT.position);

            if (dist <= despawnDistance)
            {
                outOfRangeSince.Remove(inst);
                continue;
            }

            if (!outOfRangeSince.ContainsKey(inst))
                outOfRangeSince[inst] = Time.time;

            if (Time.time - outOfRangeSince[inst] < despawnLingerSeconds)
                continue;

            // TODO(DinoBrain FSM): once San's AI FSM exists, also skip despawn while this
            // instance is in a Chase or Attack state (an actively-engaged dino shouldn't
            // vanish out from under the player mid-encounter). There is no such state to
            // query yet, so this always evaluates false and never blocks despawn.
            bool isInChaseOrAttackState = false;
            if (isInChaseOrAttackState) continue;

            if (IsInCameraView(inst.transform.position)) continue; // never despawn while visible

            activeInstances.RemoveAt(i);
            outOfRangeSince.Remove(inst);
            Destroy(inst);
        }
    }

    // ---------------- Spawn / Respawn ----------------

    // Keeps emptySlotTimers in sync with (effectiveCap - activeInstances.Count): adds a
    // freshly-rolled timer for each newly-empty slot (covers startup, and despawns freeing a
    // slot the moment it happens), and trims excess timers if the effective cap shrinks.
    void ReconcileSlotTimers(int effectiveCap)
    {
        int desiredEmptySlots = Mathf.Max(0, effectiveCap - activeInstances.Count);

        while (emptySlotTimers.Count < desiredEmptySlots)
            emptySlotTimers.Add(RollSlotSpawnTime());

        while (emptySlotTimers.Count > desiredEmptySlots)
            emptySlotTimers.RemoveAt(emptySlotTimers.Count - 1);
    }

    void UpdateSpawns(Transform playerT, int effectiveCap)
    {
        // Every empty slot's timer is independent - check all of them each tick so multiple
        // slots can spawn in the same frame if their timers happen to elapse together.
        for (int i = emptySlotTimers.Count - 1; i >= 0; i--)
        {
            if (activeInstances.Count >= effectiveCap) break;
            if (Time.time < emptySlotTimers[i]) continue;

            bool spawned = SpawnOne(playerT);
            if (spawned)
            {
                // Slot is filled now - it's no longer an empty slot with a pending timer.
                emptySlotTimers.RemoveAt(i);
            }
            else
            {
                // No valid spawn point this attempt - slot stays empty, reroll its timer.
                emptySlotTimers[i] = RollSlotSpawnTime();
            }
        }
    }

    float RollSlotSpawnTime()
    {
        return Time.time + UnityEngine.Random.Range(respawnDelayMin, respawnDelayMax);
    }

    bool SpawnOne(Transform playerT)
    {
        if (biomeDef.spawnTable == null) return false;

        GameObject prefab = biomeDef.spawnTable.GetWeightedRandomPrefab();
        if (prefab == null) return false;

        bool isTRex = prefab.name.IndexOf("TRex", StringComparison.OrdinalIgnoreCase) >= 0;

        Vector3? point = TryFindSpawnPoint(playerT.position, isTRex);
        if (point == null) return false; // no valid point this attempt; this slot's timer gets rerolled

        GameObject instance = Instantiate(prefab, point.Value, Quaternion.identity);
        activeInstances.Add(instance);
        return true;
    }

    Vector3? TryFindSpawnPoint(Vector3 origin, bool exemptFromCampfireSafeRadius)
    {
        Vector3? lastValidButVisible = null;

        for (int attempt = 0; attempt < Mathf.Max(1, frustumAvoidAttempts); attempt++)
        {
            Vector3? candidate = SampleAnnulusOnNavMesh(origin);
            if (candidate == null) continue;

            if (!exemptFromCampfireSafeRadius && campfireSite != null &&
                Vector3.Distance(candidate.Value, campfireSite.position) < campfireSafeRadius)
            {
                continue; // inside the campfire safe zone; resample
            }

            if (!IsInCameraView(candidate.Value))
                return candidate.Value; // ideal candidate: on navmesh, safe, out of view

            lastValidButVisible = candidate; // valid + safe, just in view - keep as last resort
        }

        // Couldn't bias out of view within the attempt budget - spawn anyway rather than
        // stall, using the last candidate that at least passed the navmesh + safety checks.
        return lastValidButVisible;
    }

    Vector3? SampleAnnulusOnNavMesh(Vector3 origin)
    {
        for (int i = 0; i < Mathf.Max(1, navMeshSampleAttempts); i++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float radius = UnityEngine.Random.Range(spawnRadiusMin, spawnRadiusMax);
            Vector3 candidate = origin + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleMaxDistance, NavMesh.AllAreas))
                return hit.position;
        }
        return null;
    }

    // ---------------- Helpers ----------------

    Transform ResolvePlayer()
    {
        if (player != null) return player;
        Camera cam = ResolveCamera();
        return cam != null ? cam.transform : null;
    }

    Camera ResolveCamera()
    {
        if (viewCamera != null) return viewCamera;
        return Camera.main;
    }

    bool IsInCameraView(Vector3 worldPos)
    {
        Camera cam = ResolveCamera();
        if (cam == null) return false;

        Vector3 vp = cam.WorldToViewportPoint(worldPos);
        if (vp.z <= 0f) return false; // behind the camera

        return vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
    }
}
