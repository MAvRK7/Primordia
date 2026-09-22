using UnityEngine;

public class DinoAlpha : MonoBehaviour
{
    public DinoProfile profile;

    void Start()
    {
        if (profile == null || !profile.isAlpha) return;

        // bigger body — the only thing this script still does at runtime
        transform.localScale *= profile.alphaScaleMult;

        // NOTE: alpha colouring is authored in the editor now (alpha prefabs use
        // their own materials), so the old runtime tint loop was removed — it
        // overwrote those materials and also leaked a material instance per
        // renderer. profile.alphaTint is left in place but is no longer read.
    }
}