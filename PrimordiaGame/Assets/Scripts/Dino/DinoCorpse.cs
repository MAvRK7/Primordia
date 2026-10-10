using UnityEngine;
using System.Collections.Generic;

// Corpses yield loot after three knife strikes, or expire without loot after ten minutes.
public class DinoCorpse : MonoBehaviour
{
    public DinoProfile profile;
    public bool IsButchered { get; private set; }
    public int RemainingHits { get; private set; } = 3;
    public bool CanButcher => initialized && health != null && health.IsDead && !IsButchered;

    private DinoLoot loot;
    private Animator animator;
    private float diedAt;
    readonly List<(SkinnedMeshRenderer renderer, Mesh mesh, MeshCollider collider)> surfaces = new();
    bool poseFrozen;
    bool initialized;
    DinoHealth health;

    // Called by DinoHealth.Die().
    public void Init(DinoProfile prof, DinoLoot lootRef, Animator anim)
    {
        if (initialized) return;
        health = GetComponent<DinoHealth>();
        if (health == null || !health.IsDead) return;
        initialized = true;
        profile = prof;
        loot = lootRef;
        animator = anim;
        diedAt = Time.time;
        var body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        foreach (var corpseBody in GetComponentsInChildren<Rigidbody>(true))
        {
            corpseBody.isKinematic = true;
            corpseBody.useGravity = false;
        }
        // Bake the actual surface in its death pose. A bounds box includes empty
        // space around tails and legs, which must not offer or count knife hits.
        foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!renderer.enabled || renderer.sharedMesh == null) continue;
            var mesh = new Mesh { name = name + " Corpse Surface" };
            var collider = renderer.gameObject.AddComponent<MeshCollider>();
            surfaces.Add((renderer, mesh, collider));
        }
        foreach (var filter in GetComponentsInChildren<MeshFilter>())
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || !renderer.enabled || filter.sharedMesh == null) continue;
            filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
        }
        if (animator != null) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        UpdateSurfaces();
        Destroy(gameObject, 600f);
    }

    void LateUpdate()
    {
        if (!initialized || poseFrozen) return;
        UpdateSurfaces();
        poseFrozen = animator == null || !animator.enabled;
    }

    void UpdateSurfaces()
    {
        foreach (var surface in surfaces)
        {
            // Compensate for the imported renderer scale. Without this the
            // renderer transform scales the baked vertices a second time.
            surface.renderer.BakeMesh(surface.mesh, true);
            surface.collider.sharedMesh = null;
            surface.collider.sharedMesh = surface.mesh;
        }
    }

    void OnDestroy()
    {
        foreach (var surface in surfaces) Destroy(surface.mesh);
    }

    void Update()
    {
        // Freeze the Animator after the death animation has finished playing,
        // so the corpse doesn't keep chewing CPU idling in the Dead state.
        if (initialized && animator != null && animator.enabled && profile != null &&
            profile.corpseAnimFreezeDelay > 0f &&
            Time.time - diedAt >= profile.corpseAnimFreezeDelay)
        {
            animator.enabled = false;
        }
    }

    public bool HitWithKnife(Vector3 point, Vector3 normal)
    {
        if (!CanButcher) return false;
        RemainingHits--;
        CombatImpactFeedback.ButcherHit(point, normal, RemainingHits);
        if (RemainingHits > 0) return true;
        IsButchered = true;

        if (loot != null) loot.DropLoot();

        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }
}
