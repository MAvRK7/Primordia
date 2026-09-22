using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BiomeFogSettings
{
    // Matches this project's existing scene fog setup: RenderSettings.fogMode = ExponentialSquared,
    // which uses color + density (start/end distance only apply to Linear mode and aren't used here).
    public Color color = new Color(0.5f, 0.5f, 0.5f, 1f);
    public float density = 0.01f;
}

[System.Serializable]
public class BiomeAmbientSettings
{
    // Matches this project's existing ambient setup: RenderSettings.ambientMode = Skybox
    // (tri-light sky/equator/ground), rather than a single flat ambient color.
    public Color skyColor = new Color(0.212f, 0.227f, 0.259f, 1f);
    public Color equatorColor = new Color(0.114f, 0.125f, 0.133f, 1f);
    public Color groundColor = new Color(0.047f, 0.043f, 0.035f, 1f);
    [Range(0f, 8f)]
    public float intensity = 1f;
}

// Stub for a future alpha-territory system. Intentionally left empty -
// alpha spawning/territory logic is explicitly out of scope for this pass.
[System.Serializable]
public class AlphaTerritoryRef
{
}

[CreateAssetMenu(fileName = "BiomeDef", menuName = "Spawning/BiomeDef")]
public class BiomeDef : ScriptableObject
{
    [Header("Identity")]
    public string id;

    [Header("Fog")]
    public BiomeFogSettings fogDay = new BiomeFogSettings();
    public BiomeFogSettings fogNight = new BiomeFogSettings();

    [Header("Ambient")]
    public BiomeAmbientSettings ambient = new BiomeAmbientSettings();

    [Header("Spawning")]
    public SpawnTable spawnTable;
    public int cap = 12;

    [Header("Alpha (future - not implemented yet)")]
    public List<AlphaTerritoryRef> alphaList = new List<AlphaTerritoryRef>();

    [Header("Audio")]
    public AudioClip ambientAudioRef;
}
