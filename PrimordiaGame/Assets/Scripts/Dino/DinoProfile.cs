using UnityEngine;

public enum DinoBehaviour
{
    PredatorHuntsPlayer,      // both predator values now auto-target; kept for compatibility
    PredatorHuntsHerbivores,
    PassiveRetaliator,        // ignores everyone until hit, then retaliates
    Flees                     // skittish; flees on sense, never attacks
}

[CreateAssetMenu(fileName = "DinoProfile", menuName = "Dino/DinoProfile")]
public class DinoProfile : ScriptableObject
{
    [System.Serializable]
    public class DropEntry
    {
        public string partName = "Bone";
        public int minAmount = 1;
        public int maxAmount = 1;
        [Range(0f, 1f)] public float chance = 1f;   // 1 = guaranteed, <1 = rare roll
        public bool isRare = false;                 // rares forced to 100% on alphas
    }

    [Header("Behaviour")]
    public DinoBehaviour behaviour;

    [Tooltip("Predators only. If true, hunts the player first (apex behaviour). " +
             "If false, hunts herbivores first but still switches to the player when sensed.")]
    public bool prefersPlayer = true;

    [Header("Sight")]
    public float sightRange = 18f;
    public float sightAngle = 70f;      // half-angle each side of facing

    [Header("Hearing")]
    public float hearingRadius = 18f;
    public float noiseMemory = 3f;      // how long a noise event stays 'fresh'

    [Header("Aggro / Give-up")]
    public float giveUpTimer = 5f;      // seconds since last SENSED before abandoning
    public float leashRange = 35f;      // max distance from spawn before forced give-up

    [Header("Combat")]
    public float attackRange = 0.3f;    // desired gap between bodies
    public float attackCooldown = 1.5f;
    public float attackDamage = 18f;

    [Header("Movement")]
    public float moveSpeed = 3f;

    [Header("Stats")]
    public float health = 45f;
    public int xpReward = 35;

    [Header("Loot Drops")]
    public DropEntry[] drops;

    [Header("Alpha")]
    public bool isAlpha = false;        // alphas force all rare drops to 100%
    public float alphaXpMult = 3f;   // alphas grant 3x XP
    public float alphaHealthMult = 2.5f;
    public float alphaDamageMult = 1.8f;
    public float alphaScaleMult  = 1.4f;
    public Color alphaTint = new Color(0.6f, 0.1f, 0.1f);  // dark red — tune per taste       // alphas force all rare drops to 100%
}