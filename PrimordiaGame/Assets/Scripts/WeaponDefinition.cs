using UnityEngine;

public enum WeaponType
{
    Melee,
    Gun,
}

public enum AmmoType
{
    None,
    Light,
    Heavy,
    Shell,
}

/// <summary>
/// Designer-editable weapon tuning shared by the runtime weapon components.
/// </summary>
[CreateAssetMenu(fileName = "Weapon", menuName = "Primordia/Weapon Definition")]
public sealed class WeaponDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] string m_WeaponId = "weapon";
    [SerializeField] string m_DisplayName = "Weapon";
    [SerializeField] WeaponType m_WeaponType;
    [SerializeField, Min(1)] int m_Tier = 1;

    [Header("Combat")]
    [SerializeField, Min(0f)] float m_Damage = 10f;
    [SerializeField, Min(0.01f)] float m_Range = 1f;
    [SerializeField, Min(0.01f)] float m_AttacksPerSecond = 1f;
    [SerializeField, Min(0f)] float m_NoiseRange = 3f;

    [Header("Gun")]
    [SerializeField] AmmoType m_AmmoType = AmmoType.None;
    [SerializeField, Min(0)] int m_MagazineCapacity;
    [SerializeField] bool m_Automatic;

    public string WeaponId => m_WeaponId;
    public string DisplayName => m_DisplayName;
    public WeaponType Type => m_WeaponType;
    public int Tier => m_Tier;
    public float Damage => m_Damage;
    public float Range => m_Range;
    public float AttacksPerSecond => m_AttacksPerSecond;
    public float NoiseRange => m_NoiseRange;
    public AmmoType AmmoType => m_AmmoType;
    public int MagazineCapacity => m_MagazineCapacity;
    public bool Automatic => m_Automatic;

    void OnValidate()
    {
        m_Tier = Mathf.Max(1, m_Tier);
        m_Damage = Mathf.Max(0f, m_Damage);
        m_Range = Mathf.Max(0.01f, m_Range);
        m_AttacksPerSecond = Mathf.Max(0.01f, m_AttacksPerSecond);
        m_NoiseRange = Mathf.Max(0f, m_NoiseRange);

        if (m_WeaponType == WeaponType.Melee)
        {
            m_AmmoType = AmmoType.None;
            m_MagazineCapacity = 0;
            m_Automatic = false;
        }
        else
        {
            m_MagazineCapacity = Mathf.Max(1, m_MagazineCapacity);
        }
    }
}
