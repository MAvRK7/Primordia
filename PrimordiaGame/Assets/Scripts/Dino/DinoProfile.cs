using UnityEngine;

public enum DinoBehaviour
{
    PredatorHuntsPlayer,      // kept for compatibility; target now driven by priority fields
    PredatorHuntsHerbivores,
    PassiveRetaliator,        // ignores everyone until hit, then retaliates
    Flees                     // skittish; flees on sense, never attacks
}

[CreateAssetMenu(fileName = "DinoProfile", menuName = "Dino/DinoProfile")]
public class DinoProfile : ScriptableObject
{
    public enum TargetType { None, Player, Herbivore }

    [Header("Behaviour")]
    public DinoBehaviour behaviour;

    [Header("Target Priority")]
    public TargetType primaryTarget = TargetType.Player;
    public TargetType secondaryTarget = TargetType.None;

    [Header("Sight")]
    public float sightRange = 18f;
    public float sightAngle = 70f;      // half-angle each side of facing

    [Header("Hearing")]
    public float hearingRadius = 18f;
    public float noiseMemory = 3f;      // how long a noise event stays 'fresh'

    [Header("Aggro / Give-up")]
    public float giveUpTimer = 5f;      // seconds since last SENSED (sight or hearing) before abandoning
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
}