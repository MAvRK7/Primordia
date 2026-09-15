using System.Collections.Generic;
using UnityEngine;

// Lightweight, OPTIONAL performance manager for dino AI on Quest 3.
//
// Two knobs:
//   1) Distance cap  — dinos beyond `activeDistance` from the player go dormant
//                      (they stop running AI entirely; wake back up on approach).
//   2) Time-slicing  — active dinos run their heavy sensing / target selection
//                      on a rotating subset of frames only. Movement (Act)
//                      keeps running every frame so motion stays smooth.
//
// Additive by design: if no DinoAIManager exists in the scene, every DinoAI
// behaves exactly as before. Nothing on the dino side is required.
public class DinoAIManager : MonoBehaviour
{
    public static DinoAIManager Instance { get; private set; }

    [Header("Distance cap")]
    [Tooltip("Dinos farther than this from the player go dormant (idle) until the player approaches.")]
    public float activeDistance = 60f;
    [Tooltip("Hard ceiling on how many dinos may be fully active at once. 0 = unlimited.")]
    public int maxActive = 24;
    [Tooltip("How often (seconds) to re-evaluate which dinos are active. Cheap; keep small.")]
    public float activeReevaluateInterval = 0.5f;

    [Header("Time-slicing")]
    [Tooltip("How many frame buckets to spread sensing across. 4 = each dino senses every 4th frame.")]
    [Min(1)] public int senseFrameBuckets = 4;

    [Header("Player")]
    [Tooltip("Optional. Auto-resolved from a GameObject tagged 'Player' if left null.")]
    public Transform player;

    private readonly List<DinoAI> dinos = new List<DinoAI>();
    private readonly HashSet<DinoAI> activeSet = new HashSet<DinoAI>();
    private float nextReevaluateTime;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (player == null)
        {
            GameObject pgo = GameObject.FindWithTag("Player");
            if (pgo != null) player = pgo.transform;
        }
    }

    public void Register(DinoAI dino)
    {
        if (dino == null || dinos.Contains(dino)) return;
        dinos.Add(dino);
        // Default to active so a freshly-spawned dino runs at least until we re-evaluate.
        activeSet.Add(dino);
    }

    public void Unregister(DinoAI dino)
    {
        dinos.Remove(dino);
        activeSet.Remove(dino);
    }

    void Update()
    {
        if (Time.time < nextReevaluateTime) return;
        nextReevaluateTime = Time.time + Mathf.Max(0.05f, activeReevaluateInterval);
        Reevaluate();
    }

    // Recompute which dinos are close enough to run and, if we're over the cap,
    // keep only the closest maxActive.
    void Reevaluate()
    {
        if (player == null)
        {
            GameObject pgo = GameObject.FindWithTag("Player");
            if (pgo != null) player = pgo.transform;
        }

        activeSet.Clear();

        // No player yet → let everyone run (fail-open, matches "no manager" behaviour).
        if (player == null)
        {
            for (int i = 0; i < dinos.Count; i++)
                if (dinos[i] != null) activeSet.Add(dinos[i]);
            return;
        }

        Vector3 pp = player.position;
        float cap = activeDistance * activeDistance;

        // First pass: everyone within the distance cap is a candidate.
        List<DinoAI> candidates = new List<DinoAI>(dinos.Count);
        for (int i = 0; i < dinos.Count; i++)
        {
            DinoAI d = dinos[i];
            if (d == null) continue;
            float sq = (d.transform.position - pp).sqrMagnitude;
            if (sq <= cap) candidates.Add(d);
        }

        // Second pass: if we have a hard cap, keep only the nearest maxActive.
        if (maxActive > 0 && candidates.Count > maxActive)
        {
            candidates.Sort((a, b) =>
            {
                float da = (a.transform.position - pp).sqrMagnitude;
                float db = (b.transform.position - pp).sqrMagnitude;
                return da.CompareTo(db);
            });
            candidates.RemoveRange(maxActive, candidates.Count - maxActive);
        }

        for (int i = 0; i < candidates.Count; i++) activeSet.Add(candidates[i]);
    }

    // A dino is active if it's inside the distance cap and (if the cap is on) among the nearest maxActive.
    public bool IsActive(DinoAI dino)
    {
        return activeSet.Contains(dino);
    }

    // Time-slice heavy sensing: each dino only senses on its assigned frame bucket.
    // Movement still runs every frame (this only gates the Think() pass).
    public bool MaySense(DinoAI dino)
    {
        if (senseFrameBuckets <= 1) return true;
        int bucket = (dino.GetInstanceID() & 0x7fffffff) % senseFrameBuckets;
        return (Time.frameCount % senseFrameBuckets) == bucket;
    }
}
