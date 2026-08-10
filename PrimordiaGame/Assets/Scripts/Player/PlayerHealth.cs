using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    private float currentHealth;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        Debug.Log("Player took " + amount + " damage. Health: " + currentHealth);

        if (currentHealth <= 0)
        {
            Debug.Log("Player died!");
            currentHealth = maxHealth;   // simple respawn: reset health for now
        }
    }
}