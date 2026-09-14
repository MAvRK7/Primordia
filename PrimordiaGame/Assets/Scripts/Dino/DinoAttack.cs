using UnityEngine;

// Modular attack execution, extracted verbatim from DinoAI's old Attack state.
// Behaviour is identical: same cooldown, same damage (incl. alpha multiplier),
// same animator "Attack" trigger, same PlayerHealth / DinoHealth calls.
//
// DinoAI auto-adds this component in Start() if the prefab doesn't already have
// one, so existing prefabs keep working with no editor changes. Swap in a
// different DinoAttack-derived component to change how a species attacks.
public class DinoAttack : MonoBehaviour
{
    private DinoAI ai;
    private DinoProfile profile;
    private Animator animator;
    private AudioSource audioSource;   // optional — for the attack sound
    private float lastAttackTime = -999f;

    // Called by DinoAI.Start so this module shares the same refs.
    public void Init(DinoAI owner, DinoProfile prof, Animator anim, AudioSource src)
    {
        ai = owner;
        profile = prof;
        animator = anim;
        audioSource = src;
    }

    // Attempt an attack against the target. Respects cooldown; a no-op until ready.
    // Returns true if an attack actually fired this call.
    public bool TryAttack(Transform target)
    {
        if (target == null || profile == null) return false;
        if (Time.time - lastAttackTime < profile.attackCooldown) return false;

        lastAttackTime = Time.time;

        if (animator != null) animator.SetTrigger("Attack");

        float dmg = profile.attackDamage;
        if (profile.isAlpha) dmg *= profile.alphaDamageMult;

        PlayerHealth php = target.GetComponent<PlayerHealth>();
        DinoHealth dh = target.GetComponent<DinoHealth>();
        if (php != null) php.TakeDamage(dmg);
        if (dh != null) dh.TakeDamage(dmg, transform);

        // attack sound (optional; silent if no clip or no AudioSource)
        if (profile.attackSound != null && audioSource != null)
            audioSource.PlayOneShot(profile.attackSound);

        return true;
    }
}
