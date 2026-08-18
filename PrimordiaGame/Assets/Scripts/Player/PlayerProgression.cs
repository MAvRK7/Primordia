using UnityEngine;

public class PlayerProgression : MonoBehaviour
{
    public int currentXP = 0;
    public int currentLevel = 1;

    // XP needed to reach the next level (simple curve; tune later)
    public int XPForNextLevel => currentLevel * 100;

    public void AddXP(int amount)
    {
        currentXP += amount;
        Debug.Log($"Gained {amount} XP. Total: {currentXP}");

        // level up while we have enough XP
        while (currentXP >= XPForNextLevel)
        {
            currentXP -= XPForNextLevel;
            currentLevel++;
            Debug.Log($"LEVEL UP! Now level {currentLevel}");
        }
    }
}