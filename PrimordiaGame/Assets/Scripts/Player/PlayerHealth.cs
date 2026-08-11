using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public bool IsDead { get; private set; }
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
            IsDead = true;
            Debug.Log("Player died!");
        }
    }
}