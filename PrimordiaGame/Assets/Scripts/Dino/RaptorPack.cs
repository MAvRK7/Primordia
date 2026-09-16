using UnityEngine;
using System.Collections.Generic;

public class RaptorPack : MonoBehaviour
{
    // The prey the whole pack is currently hunting (null = no target).
    public Transform SharedTarget { get; private set; }

    private List<DinoAI> members = new List<DinoAI>();

    public void Register(DinoAI raptor)
    {
        if (!members.Contains(raptor)) members.Add(raptor);
    }

    // A member reports it sensed prey -> becomes the pack's shared target.
    // `reporter` is the member that spotted the prey; used only to voice the
    // rally cry through the right dino. Optional so old callers still compile.
    //
    // The rally sound plays ONCE on the null -> target transition. Because
    // ClearTarget() sets SharedTarget back to null, the very next report after
    // a lost target counts as a fresh rally and the sound plays again. No
    // extra flag needed — the null state IS the reset.
    public void ReportTarget(Transform prey, DinoAI reporter = null)
    {
        if (prey == null) return;
        // A corpse is never prey — without this the pack could rally onto a body
        // that died the same frame and every member would stand over it.
        if (IsDead(prey)) return;

        bool freshRally = SharedTarget == null;
        SharedTarget = prey;

        if (freshRally) PlayFormSound(reporter);
    }

    // Called when the target dies or is lost -> pack stands down.
    public void ClearTarget()
    {
        SharedTarget = null;
    }

    // Safety net for the pack-commitment rule in DinoAI: members stay locked on
    // SharedTarget while it is alive, so the pack itself must drop a target the
    // instant it dies, even if no member reports it this frame.
    void Update()
    {
        if (SharedTarget != null && IsDead(SharedTarget)) SharedTarget = null;
    }

    // Local copy of DinoAI's death test — the pack has no DinoAI of its own to
    // ask, and destroyed objects read as null here too (Unity fake-null).
    bool IsDead(Transform t)
    {
        if (t == null) return true;
        if (t.GetComponent<DinoCorpse>() != null) return true;
        PlayerHealth php = t.GetComponent<PlayerHealth>();
        if (php != null && php.IsDead) return true;
        DinoHealth dh = t.GetComponent<DinoHealth>();
        if (dh != null && dh.IsDead) return true;
        return false;
    }

    // Play the pack-formation cry through exactly ONE dino so we don't get a
    // stacked overlapping mess from every member firing at once.
    void PlayFormSound(DinoAI reporter)
    {
        DinoAI speaker = reporter;
        // Fall back to the first live member if we weren't given a reporter
        // (or the reporter has since been destroyed).
        if (speaker == null)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] != null) { speaker = members[i]; break; }
            }
        }
        if (speaker == null || speaker.profile == null) return;

        // DinoAI.PlayClip is null-safe on both clip and AudioSource — no clip
        // or no AudioSource on the dino = silent, no errors.
        speaker.PlayClip(speaker.profile.packFormSound);
    }
}
