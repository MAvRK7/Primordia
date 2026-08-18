using UnityEngine;

public class DinoAlpha : MonoBehaviour
{
    public DinoProfile profile;

    void Start()
    {
        if (profile == null || !profile.isAlpha) return;

        // bigger body
        transform.localScale *= profile.alphaScaleMult;

        // tint all the dino's materials so it reads as an alpha
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            foreach (Material m in r.materials)
                m.color = profile.alphaTint;
    }
}