using System.Collections.Generic;
using UnityEngine;

// Lightweight, OPTIONAL performance manager for dino AI on Quest 3.
//
// Knobs:
//   1) Distance cap  — dinos beyond `activeDistance` from the player go dormant
//                      (stop running AI; wake back up on approach).
//   2) Time-slicing  — active dinos run heavy sensing / target selection on a
//                      rotating subset of frames. Movement still runs every frame.
//   3) Animator cull — dinos that are off every camera's frustum OR beyond
//                      `cullDistance` get their Animator disabled (big CPU/GPU
//                      saving on Quest), re-enabled when visible/near.
//   4) Despawn       — dinos beyond `despawnDistance` are Destroyed, EXCEPT ones
//                      in combat or that the player is currently looking at.
//
// Additive by design: if no DinoAIManager exists in the scene, every DinoAI
// behaves exactly as before. Nothing on the dino side is required.
// Establish Instance before scene dinosaurs register in OnEnable.
[DefaultExecutionOrder(-50)]
public class DinoAIManager : MonoBehaviour
{
    public static DinoAIManager Instance { get; private set; }

    [Header("Distance cap (AI sleep)")]
    [Tooltip("Dinos farther than this from the player go dormant (idle) until the player approaches.")]
    public float activeDistance = 60f;
    [Tooltip("Hard ceiling on how many dinos may be fully active at once. 0 = unlimited.")]
    public int maxActive = 24;
    [Tooltip("How often (seconds) to re-evaluate active/cull/despawn. Cheap; keep small.")]
    public float reevaluateInterval = 0.5f;

    [Header("Time-slicing")]
    [Tooltip("How many frame buckets to spread sensing across. 4 = each dino senses every 4th frame.")]
    [Min(1)] public int senseFrameBuckets = 4;

    [Header("Animator culling")]
    [Tooltip("Enable off-screen / far Animator disabling.")]
    public bool enableAnimatorCulling = true;
    [Tooltip("Beyond this distance from the player the Animator is disabled regardless of visibility.")]
    public float cullDistance = 45f;

    [Header("Despawn")]
    [Tooltip("Enable distance despawn. Dinos in combat or being looked at are never despawned.")]
    public bool enableDespawn = false;
    [Tooltip("Dinos beyond this distance from the player are Destroyed (unless in combat / looked at).")]
    public float despawnDistance = 120f;
    [Tooltip("Dot-product threshold for 'player is looking at it'. 0.9 ≈ within ~25° of view centre.")]
    [Range(0f, 1f)] public float lookAtDot = 0.9f;

    [Header("Player")]
    [Tooltip("Optional. Auto-resolved from a GameObject tagged 'Player' if left null.")]
    public Transform player;
    [Tooltip("Optional. Camera used for look-at and off-screen tests. Defaults to Camera.main.")]
    public Camera viewCamera;

    [Header("Environment hooks (read by DinoAI sight checks)")]
    [Tooltip("Set true at night to shrink dino sight by profile.nightSightMultiplier.")]
    public bool isNight = false;
    [Tooltip("Set true while the player crouches to shrink dino sight by profile.crouchSightMultiplier.")]
    public bool playerIsCrouching = false;

    // Null-safe static accessors so DinoAI can read the hooks whether or not a
    // manager exists. No manager -> both false -> no multiplier applied.
    public static bool IsNightNow => Instance != null && Instance.isNight;
    public static bool IsPlayerCrouchingNow => Instance != null && Instance.playerIsCrouching;

    private readonly List<DinoAI> dinos = new List<DinoAI>();
    private readonly HashSet<DinoAI> activeSet = new HashSet<DinoAI>();
    private readonly Plane[] frustum = new Plane[6];
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
        ResolvePlayer();
        if (viewCamera == null) viewCamera = Camera.main;
    }

    void ResolvePlayer()
    {
        if (player != null) return;
        GameObject pgo = GameObject.FindWithTag("Player");
        if (pgo != null) player = pgo.transform;
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
        nextReevaluateTime = Time.time + Mathf.Max(0.05f, reevaluateInterval);

        ResolvePlayer();
        if (viewCamera == null) viewCamera = Camera.main;

        Reevaluate();
        ApplyAnimatorCullingAndDespawn();
    }

    // Recompute which dinos are close enough to run and, if we're over the cap,
    // keep only the closest maxActive.
    void Reevaluate()
    {
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

        List<DinoAI> candidates = new List<DinoAI>(dinos.Count);
        for (int i = 0; i < dinos.Count; i++)
        {
            DinoAI d = dinos[i];
            if (d == null) continue;
            float sq = (d.transform.position - pp).sqrMagnitude;
            if (sq <= cap) candidates.Add(d);
        }

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

    // Disable Animators that are off-screen or far; despawn the very-far, safely.
    void ApplyAnimatorCullingAndDespawn()
    {
        bool haveFrustum = viewCamera != null;
        if (haveFrustum) GeometryUtility.CalculateFrustumPlanes(viewCamera, frustum);

        Vector3 pp = player != null ? player.position : (viewCamera != null ? viewCamera.transform.position : Vector3.zero);
        bool havePlayer = player != null || viewCamera != null;

        float cullSq = cullDistance * cullDistance;
        float despawnSq = despawnDistance * despawnDistance;

        // iterate backwards so a despawn can remove from the list safely
        for (int i = dinos.Count - 1; i >= 0; i--)
        {
            DinoAI d = dinos[i];
            if (d == null) { dinos.RemoveAt(i); continue; }

            float distSq = havePlayer ? (d.transform.position - pp).sqrMagnitude : 0f;
            bool visible = IsVisible(d, haveFrustum);

            // ---- Despawn (guarded) ----
            if (enableDespawn && havePlayer && distSq > despawnSq)
            {
                bool lookedAt = IsLookedAt(d);
                if (!d.InCombat && !lookedAt)
                {
                    dinos.RemoveAt(i);
                    activeSet.Remove(d);
                    Destroy(d.gameObject);
                    continue;
                }
                // in combat or being watched → keep it, fall through to culling
            }

            // ---- Animator culling ----
            if (enableAnimatorCulling)
            {
                Animator anim = d.AnimatorRef;
                if (anim != null)
                {
                    bool far = havePlayer && distSq > cullSq;
                    bool shouldAnimate = visible && !far;
                    if (anim.enabled != shouldAnimate) anim.enabled = shouldAnimate;
                }
            }
        }
    }

    bool IsVisible(DinoAI d, bool haveFrustum)
    {
        if (!haveFrustum) return true;   // no camera → assume visible (fail-open)

        // Prefer a renderer's world bounds; fall back to a point test.
        Renderer r = d.GetComponentInChildren<Renderer>();
        if (r != null) return GeometryUtility.TestPlanesAABB(frustum, r.bounds);

        Vector3 vp = viewCamera.WorldToViewportPoint(d.transform.position);
        return vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
    }

    bool IsLookedAt(DinoAI d)
    {
        if (viewCamera == null) return false;
        Vector3 to = d.transform.position - viewCamera.transform.position;
        if (to.sqrMagnitude < 0.0001f) return true;
        to.Normalize();
        return Vector3.Dot(viewCamera.transform.forward, to) >= lookAtDot;
    }

    // ---- Active / sensing gates queried by DinoAI ----

    public bool IsActive(DinoAI dino) => activeSet.Contains(dino);

    public bool MaySense(DinoAI dino)
    {
        if (senseFrameBuckets <= 1) return true;
        int bucket = (dino.GetInstanceID() & 0x7fffffff) % senseFrameBuckets;
        return (Time.frameCount % senseFrameBuckets) == bucket;
    }
}
