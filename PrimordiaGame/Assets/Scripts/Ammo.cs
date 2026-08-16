using UnityEngine;

/// <summary>
/// A reserve stack that a gun can draw from when it reloads.
/// This can move onto the player inventory once that system is implemented.
/// </summary>
public class Ammo : Item
{
    [SerializeField] AmmoType m_AmmoType = AmmoType.Light;
    [SerializeField, Min(0)] int m_Rounds = 18;

    public AmmoType Type => m_AmmoType;
    public int Rounds => m_Rounds;

    public event System.Action<int> changed;

    public int Take(AmmoType requestedType, int requestedRounds)
    {
        if (requestedType != m_AmmoType || requestedRounds <= 0 || m_Rounds <= 0)
            return 0;

        var amount = Mathf.Min(requestedRounds, m_Rounds);
        m_Rounds -= amount;
        changed?.Invoke(m_Rounds);
        return amount;
    }

    public void Add(AmmoType addedType, int amount)
    {
        if (addedType != m_AmmoType || amount <= 0)
            return;

        m_Rounds += amount;
        changed?.Invoke(m_Rounds);
    }

    public void Configure(AmmoType ammoType, int startingRounds)
    {
        m_AmmoType = ammoType;
        m_Rounds = Mathf.Max(0, startingRounds);
        changed?.Invoke(m_Rounds);
    }
}
