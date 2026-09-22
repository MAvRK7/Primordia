using UnityEngine;
using UnityEngine.AI;

/// <summary>Uses the same campsite bounds to block dinosaur paths and hide occupants.</summary>
[DisallowMultipleComponent]
public sealed class CampsiteSafeZone : MonoBehaviour
{
    [SerializeField] Vector3 m_Center = new Vector3(0f, 0f, 4f);
    [SerializeField] Vector3 m_Size = new Vector3(60f, 300f, 60f);

    static CampsiteSafeZone instance;
    NavMeshObstacle obstacle;

    void OnEnable()
    {
        instance = this;
        if (obstacle == null)
        {
            // Carving blocks navigation without a physical wall blocking the player.
            obstacle = gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = m_Center;
            obstacle.size = m_Size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
        }
        obstacle.enabled = true;
    }

    void OnDisable()
    {
        if (instance == this) instance = null;
        if (obstacle != null) obstacle.enabled = false;
    }

    public static bool Contains(Vector3 position)
    {
        return instance != null && new Bounds(instance.m_Center, instance.m_Size)
            .Contains(instance.transform.InverseTransformPoint(position));
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(m_Center, m_Size);
    }
}
