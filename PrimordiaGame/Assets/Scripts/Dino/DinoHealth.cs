using UnityEngine;
using UnityEngine.AI;

public class DinoHealth : MonoBehaviour
{
    public DinoProfile profile;      // same profile the DinoAI uses
    private float currentHealth;
    private Animator animator;
    private bool isDead;

    void Start()
    {
        currentHealth = profile.health;
        animator = GetComponentInChildren<Animator>();
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;                       // already dead, ignore
        currentHealth -= amount;
        Debug.Log(name + " took " + amount + ". HP: " + currentHealth);

        if (currentHealth <= 0)
            Die();
    }

    void Die()
    {
        isDead = true;
        Debug.Log(name + " died!");

        // stop the AI and movement
        GetComponent<DinoAI>().enabled = false;
        if (GetComponent<NavMeshAgent>().isOnNavMesh)
            GetComponent<NavMeshAgent>().isStopped = true;

        // play death animation
        animator.SetTrigger("Die");

        // leave the corpse for a few seconds, then remove it
        Destroy(gameObject, 5f);
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.K)) TakeDamage(9999);  // TEMP test kill
    }
}