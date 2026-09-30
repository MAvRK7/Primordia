using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class DinoSpawnGroup : MonoBehaviour
{
    [Header("Dino")]
    public GameObject dinoPrefab;
    [Min(1)] public int count = 3;
    [Min(0f)] public float scatterRadius = 10f;

    [Header("Respawn")]
    public bool enableRespawn = true;
    [Min(0f)] public float minRespawn = DinoSpawnPoint.RegularMinRespawn;
    [Min(0f)] public float maxRespawn = DinoSpawnPoint.RegularMaxRespawn;
    [Min(0f)] public float minDistanceFromPlayer;
    public Transform player;

    [Header("NavMesh")]
    [Min(0f)] public float navMeshSampleDistance = 5f;
    [Min(1)] public int navMeshSampleAttempts = 8;

    [SerializeField, HideInInspector] private GameObject prefabUsedForDefaults;

    private readonly List<SpawnSlot> slots = new List<SpawnSlot>();

    void OnValidate()
    {
        if (dinoPrefab == prefabUsedForDefaults)
            return;

        prefabUsedForDefaults = dinoPrefab;
        ApplyPrefabDefaults();
    }

    void Start()
    {
        CreateSpawnSlots();
    }

    void Update()
    {
        if (!enableRespawn)
            return;

        foreach (SpawnSlot slot in slots)
        {
            if (slot.instance != null)
                continue;

            if (slot.respawnAt < 0f)
            {
                slot.respawnAt = Time.time + Random.Range(Mathf.Min(minRespawn, maxRespawn), Mathf.Max(minRespawn, maxRespawn));
                continue;
            }

            if (Time.time >= slot.respawnAt && IsFarEnoughFromPlayer(slot.position))
                SpawnSlotDino(slot);
        }
    }

    public void ApplyPrefabDefaults()
    {
        DinoSpawnPoint.GetDefaultRespawnRange(dinoPrefab, out minRespawn, out maxRespawn);
    }

    private void CreateSpawnSlots()
    {
        if (dinoPrefab == null)
        {
            Debug.LogWarning($"{name} has no dino prefab assigned.", this);
            return;
        }

        slots.Clear();
        int slotCount = Mathf.Max(1, count);
        for (int i = 0; i < slotCount; i++)
        {
            if (!TryGetSpawnPosition(out Vector3 position))
            {
                Debug.LogWarning($"{name} could not find a NavMesh position for group slot {i + 1}.", this);
                continue;
            }

            SpawnSlot slot = new SpawnSlot(position);
            slots.Add(slot);
            SpawnSlotDino(slot);
        }
    }

    private void SpawnSlotDino(SpawnSlot slot)
    {
        slot.instance = Instantiate(dinoPrefab, slot.position, transform.rotation);
        slot.instance.transform.SetParent(transform, true);
        slot.respawnAt = -1f;
    }

    private bool TryGetSpawnPosition(out Vector3 position)
    {
        int attempts = Mathf.Max(1, navMeshSampleAttempts);
        for (int i = 0; i < attempts; i++)
        {
            Vector2 offset = Random.insideUnitCircle * scatterRadius;
            Vector3 candidate = transform.position + new Vector3(offset.x, 0f, offset.y);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                position = hit.position;
                return true;
            }
        }

        position = default;
        return false;
    }

    private bool IsFarEnoughFromPlayer(Vector3 spawnPosition)
    {
        if (minDistanceFromPlayer <= 0f)
            return true;

        Transform playerTransform = player != null ? player : Camera.main != null ? Camera.main.transform : null;
        if (playerTransform == null)
            return true;

        return Vector3.Distance(spawnPosition, playerTransform.position) >= minDistanceFromPlayer;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = DinoSpawnPoint.IsAlphaPrefab(dinoPrefab) ? new Color(1f, 0.35f, 0.1f) : new Color(0.2f, 0.8f, 1f);
        Gizmos.DrawWireSphere(transform.position, scatterRadius);
        Gizmos.DrawSphere(transform.position, 0.25f);
    }

    private sealed class SpawnSlot
    {
        public readonly Vector3 position;
        public GameObject instance;
        public float respawnAt = -1f;

        public SpawnSlot(Vector3 position)
        {
            this.position = position;
        }
    }
}
