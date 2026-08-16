using UnityEngine;

/// <summary>
/// Forwards a child trigger collider to its owning melee weapon.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class MeleeHitbox : MonoBehaviour
{
    [SerializeField] Melee m_Owner;
    [SerializeField] Collider m_HitCollider;

    public Collider HitCollider => m_HitCollider;

    void Awake()
    {
        if (m_HitCollider == null)
            m_HitCollider = GetComponent<Collider>();

        if (m_Owner == null)
            m_Owner = GetComponentInParent<Melee>();

        if (m_HitCollider != null)
            m_HitCollider.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        m_Owner?.RegisterHit(other, m_HitCollider);
    }

    public void SetActive(bool active)
    {
        if (m_HitCollider == null)
            m_HitCollider = GetComponent<Collider>();

        if (m_HitCollider != null)
            m_HitCollider.enabled = active;
    }
}
