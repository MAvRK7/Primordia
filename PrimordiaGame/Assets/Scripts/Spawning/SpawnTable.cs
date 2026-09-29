using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SpawnEntry
{
    public GameObject prefab;
    public float weight = 1f;
}

[CreateAssetMenu(fileName = "SpawnTable", menuName = "Spawning/SpawnTable")]
public class SpawnTable : ScriptableObject
{
    public List<SpawnEntry> entries = new List<SpawnEntry>();

    // Weighted random pick. Returns null if the table is empty or all weights are zero.
    public GameObject GetWeightedRandomPrefab()
    {
        float total = 0f;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].prefab == null) continue;
            total += Mathf.Max(0f, entries[i].weight);
        }
        if (total <= 0f) return null;

        float roll = Random.value * total;
        float accum = 0f;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].prefab == null) continue;
            accum += Mathf.Max(0f, entries[i].weight);
            if (roll <= accum) return entries[i].prefab;
        }

        // Floating point fallback: return the last valid entry.
        for (int i = entries.Count - 1; i >= 0; i--)
            if (entries[i].prefab != null) return entries[i].prefab;

        return null;
    }
}
