using UnityEngine;

public class DinoLoot : MonoBehaviour
{
    public DinoProfile profile;
    public float scatterRadius = 1.5f;

    public void DropLoot()
    {
        if (profile == null || profile.drops == null) return;

        foreach (DinoProfile.DropEntry entry in profile.drops)
        {
            // Alphas force their rare drops to always drop.
            float chance = (profile.isAlpha && entry.isRare) ? 1f : entry.chance;
            if (Random.value > chance) continue;

            int amount = Random.Range(entry.minAmount, entry.maxAmount + 1);
            for (int i = 0; i < amount; i++)
                SpawnPlaceholder(entry.partName);
        }
    }

    void SpawnPlaceholder(string partName)
    {
        GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        drop.name = partName;
        drop.transform.localScale = Vector3.one * 0.4f;

        // colour the cube by part type so drops are readable at a glance
        Renderer rend = drop.GetComponent<Renderer>();
        rend.material.color = ColorForPart(partName);

        Vector2 off = Random.insideUnitCircle * scatterRadius;
        drop.transform.position = transform.position + new Vector3(off.x, 0.3f, off.y);

        Rigidbody rb = drop.AddComponent<Rigidbody>();
        rb.AddForce(new Vector3(off.x, 3f, off.y), ForceMode.Impulse);
    }

    Color ColorForPart(string partName)
    {
        switch (partName)
        {
            // bulk parts
            case "Bone":            return new Color(0.93f, 0.90f, 0.78f);  // bone white
            case "Hide":            return new Color(0.55f, 0.35f, 0.20f);  // brown leather
            case "Giant Bone":      return new Color(0.80f, 0.76f, 0.62f);  // darker bone

            // signature parts
            case "Sickle Claw":     return new Color(0.85f, 0.85f, 0.90f);  // pale claw
            case "Horn":            return new Color(0.60f, 0.60f, 0.65f);  // grey horn
            case "Back-Plate":      return new Color(0.30f, 0.55f, 0.35f);  // green plate
            case "Crest":           return new Color(0.90f, 0.60f, 0.30f);  // orange crest
            case "Apex Fang":       return new Color(0.95f, 0.95f, 0.88f);  // ivory fang

            // rares — bright/saturated so they pop
            case "Alpha Pelt":      return new Color(0.60f, 0.10f, 0.60f);  // purple
            case "Flawless Horn":   return new Color(0.20f, 0.80f, 1.00f);  // cyan
            case "Pristine Crest":  return new Color(1.00f, 0.40f, 0.70f);  // pink
            case "Twin Tail-Spike": return new Color(1.00f, 0.30f, 0.30f);  // red
            case "Ancient Bone":    return new Color(1.00f, 0.85f, 0.20f);  // gold
            case "Rex Skull":       return new Color(0.10f, 0.90f, 0.50f);  // emerald

            default:                return Color.grey;                       // anything unlisted
        }
    }
}