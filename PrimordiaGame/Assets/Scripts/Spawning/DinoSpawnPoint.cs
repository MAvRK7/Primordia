using UnityEngine;
using UnityEngine.AI;

public class DinoSpawnPoint : MonoBehaviour
{
    public const float RegularMinRespawn = 60f;
    public const float RegularMaxRespawn = 120f;
    public const float AlphaMinRespawn = 600f;
    public const float AlphaMaxRespawn = 1200f;

    [Header("Dino")]
    public GameObject dinoPrefab;

    [Header("Respawn")]
    public bool enableRespawn = true;
    [Min(0f)] public float minRespawn = RegularMinRespawn;
    [Min(0f)] public float maxRespawn = RegularMaxRespawn;
    [Min(0f)] public float minDistanceFromPlayer;
    public Transform player;

    [Header("NavMesh")]
    [Min(0f)] public float navMeshSampleDistance = 5f;

    [SerializeField, HideInInspector] private GameObject prefabUsedForDefaults;

    private GameObject spawnedInstance;
    private float respawnAt = -1f;

    void OnValidate()
    {
        if (dinoPrefab == prefabUsedForDefaults)
            return;

        prefabUsedForDefaults = dinoPrefab;
        ApplyPrefabDefaults();
    }

    void Start()
    {
        SpawnDino();
    }

    void Update()
    {
        if (spawnedInstance != null || !enableRespawn)
            return;

        if (respawnAt < 0f)
        {
            respawnAt = Time.time + Random.Range(Mathf.Min(minRespawn, maxRespawn), Mathf.Max(minRespawn, maxRespawn));
            return;
        }

        if (Time.time < respawnAt || !IsFarEnoughFromPlayer())
            return;

        SpawnDino();
    }

    public void ApplyPrefabDefaults()
    {
        GetDefaultRespawnRange(dinoPrefab, out minRespawn, out maxRespawn);
    }

    public static void GetDefaultRespawnRange(GameObject prefab, out float minimum, out float maximum)
    {
        if (IsAlphaPrefab(prefab))
        {
            minimum = AlphaMinRespawn;
            maximum = AlphaMaxRespawn;
            return;
        }

        minimum = RegularMinRespawn;
        maximum = RegularMaxRespawn;
    }

    public static bool IsAlphaPrefab(GameObject prefab)
    {
        if (prefab == null)
            return false;

        DinoAI dinoAI = prefab.GetComponent<DinoAI>();
        if (dinoAI != null && dinoAI.profile != null)
            return dinoAI.profile.isAlpha;

        DinoHealth dinoHealth = prefab.GetComponent<DinoHealth>();
        return dinoHealth != null && dinoHealth.profile != null && dinoHealth.profile.isAlpha;
    }

    private void SpawnDino()
    {
        if (dinoPrefab == null)
        {
            Debug.LogWarning($"{name} has no dino prefab assigned.", this);
            return;
        }

        if (!TryGetSpawnPosition(transform.position, out Vector3 position))
        {
            Debug.LogWarning($"{name} could not find a NavMesh position near its spawn point.", this);
            return;
        }

        spawnedInstance = Instantiate(dinoPrefab, position, transform.rotation);
        spawnedInstance.transform.SetParent(transform, true);
        respawnAt = -1f;
    }

    private bool TryGetSpawnPosition(Vector3 requestedPosition, out Vector3 position)
    {
        if (NavMesh.SamplePosition(requestedPosition, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            position = hit.position;
            return true;
        }

        position = default;
        return false;
    }

    private bool IsFarEnoughFromPlayer()
    {
        if (minDistanceFromPlayer <= 0f)
            return true;

        Transform playerTransform = player != null ? player : Camera.main != null ? Camera.main.transform : null;
        if (playerTransform == null)
            return true;

        return Vector3.Distance(transform.position, playerTransform.position) >= minDistanceFromPlayer;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = IsAlphaPrefab(dinoPrefab) ? new Color(1f, 0.35f, 0.1f) : new Color(0.2f, 0.8f, 1f);
        Gizmos.DrawWireSphere(transform.position, 0.75f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
    }
}
