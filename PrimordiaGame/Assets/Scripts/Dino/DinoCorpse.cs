using UnityEngine;

// A dead dino that has NOT been butchered yet. Sits in the world after death,
// keeps the death-pose visible, and releases loot only when Butcher() is
// called (VR interactor will call it later — for now, aim at it and press B).
//
// This replaces the old "Destroy(gameObject, 5f)" auto-loot flow: death now
// only grants XP and freezes the AI; loot is a separate, explicit action.
public class DinoCorpse : MonoBehaviour
{
    public DinoProfile profile;
    public bool IsButchered { get; private set; }

    private DinoLoot loot;
    private Animator animator;
    private float diedAt;

    // Called by DinoHealth.Die().
    public void Init(DinoProfile prof, DinoLoot lootRef, Animator anim)
    {
        profile = prof;
        loot = lootRef;
        animator = anim;
        diedAt = Time.time;
    }

    void Update()
    {
        // Freeze the Animator after the death animation has finished playing,
        // so the corpse doesn't keep chewing CPU idling in the Dead state.
        if (animator != null && animator.enabled && profile != null &&
            profile.corpseAnimFreezeDelay > 0f &&
            Time.time - diedAt >= profile.corpseAnimFreezeDelay)
        {
            animator.enabled = false;
        }

        // Debug key while there's no VR interactor yet: point roughly at the
        // corpse and press B. Removed once the real trigger is wired up.
        if (!IsButchered && Input.GetKeyDown(KeyCode.B))
        {
            GameObject pgo = GameObject.FindWithTag("Player");
            if (pgo != null &&
                (pgo.transform.position - transform.position).sqrMagnitude < 9f) // within 3 m
            {
                Butcher();
            }
        }
    }

    // Public so the VR butcher trigger (added later) can call it. Idempotent:
    // calling twice will not double-drop.
    public void Butcher()
    {
        if (IsButchered) return;
        IsButchered = true;

        if (loot != null) loot.DropLoot();

        // Give the animation state / player interaction a beat to settle, then
        // remove the carcass from the world.
        float delay = profile != null ? Mathf.Max(0f, profile.corpseDespawnDelay) : 0f;
        Destroy(gameObject, delay);
    }
}
