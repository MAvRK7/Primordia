using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Creates the configured player rig at this scene's starting position.</summary>
// Spawn before dinosaur Start callbacks resolve the Player tag.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class PlayerSpawnPoint : MonoBehaviour
{
    [SerializeField] GameObject m_PlayerPrefab;

    public GameObject SpawnedPlayer { get; private set; }

    void Start()
    {
        SpawnPlayer();
    }

    public GameObject SpawnPlayer()
    {
        if (SpawnedPlayer != null)
            return SpawnedPlayer;

        if (m_PlayerPrefab == null)
        {
            Debug.LogError("PlayerSpawnPoint needs a player prefab.", this);
            return null;
        }

        SpawnedPlayer = Instantiate(m_PlayerPrefab, transform.position, transform.rotation);
        SpawnedPlayer.name = m_PlayerPrefab.name;
        SceneManager.MoveGameObjectToScene(SpawnedPlayer, gameObject.scene);
        return SpawnedPlayer;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.3f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.7f);
        Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * 1.5f);
    }
}
