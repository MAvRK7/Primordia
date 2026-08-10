using UnityEngine;

[CreateAssetMenu(fileName = "DinoProfile", menuName = "Dino/Profile")]
public class DinoProfile : ScriptableObject
{
    [Header("Sight")]
    public float sightRange = 15f;
    public float sightAngle = 60f;

    [Header("Hearing")]
    public float hearingRadius = 12f;
    public float noiseMemory = 3f;

    [Header("Attack")]
    public float attackDamage = 10f;
    public float attackRange = 2f;
    public float attackCooldown = 1.5f;

    [Header("Movement")]
    public float moveSpeed = 3.5f;

    [Header("Stats")]
    public float health = 100f;
    public int xpReward = 10;

    [Header("Behaviour")]
    public bool isPredator = true;   // predators chase; non-predators will flee
}