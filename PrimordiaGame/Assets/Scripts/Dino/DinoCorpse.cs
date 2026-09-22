using UnityEngine;

// Corpses yield loot after three knife strikes, or expire without loot after ten minutes.
public class DinoCorpse : MonoBehaviour
{
    public DinoProfile profile;
    public bool IsButchered { get; private set; }
    public int RemainingHits { get; private set; } = 3;

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
        Destroy(gameObject, 600f);
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
    }

    public void HitWithKnife()
    {
        if (IsButchered) return;
        if (--RemainingHits > 0) return;
        IsButchered = true;

        if (loot != null) loot.DropLoot();

        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
