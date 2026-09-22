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
    public void ReportTarget(Transform prey)
    {
        if (prey != null) SharedTarget = prey;
    }

    // Called when the target dies or is lost -> pack stands down.
    public void ClearTarget()
    {
        SharedTarget = null;
    }
}