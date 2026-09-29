using System;
using UnityEngine;

/// <summary>Maps DinoProfile drop names to the model prefab that drops in the world.</summary>
[CreateAssetMenu(fileName = "Loot Catalog", menuName = "Primordia/Loot Catalog")]
public class LootCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string partName;
        public GameObject prefab;
    }

    public Entry[] entries = Array.Empty<Entry>();

    // Profiles aren't consistent about case ("crest", "bone"), so match loosely.
    public GameObject PrefabFor(string partName)
    {
        foreach (Entry entry in entries)
        {
            if (entry != null && entry.prefab != null &&
                string.Equals(entry.partName, partName, StringComparison.OrdinalIgnoreCase))
                return entry.prefab;
        }
        return null;
    }
}
