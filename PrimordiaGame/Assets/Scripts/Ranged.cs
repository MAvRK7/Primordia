using UnityEngine;

public class Ranged : Item {
    Camera playerCamera;
    public float damage;
    public float weaponRange;
    public Ammo ammo;
    public Transform target;
    public Transform firePoint;

    public void Start() {
        playerCamera = Camera.main;
    }

    public bool fire() {
        
        // GameObject fired = Instantiate(ammo.gameObject, firePoint.position, firePoint.rotation);
        
        Vector3 rayOrigin = playerCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0.0f)); // Screen center
        Vector3 rayDirection = playerCamera.transform.forward; // Facing forward

        RaycastHit hitData;
        
        Debug.DrawRay(rayOrigin, rayDirection * weaponRange, Color.red, 1.0f);

        if (Physics.Raycast(rayOrigin, rayDirection, out hitData, weaponRange)) {
            // Identify the name of the hit object
            Debug.Log("Raycast hit object: " + hitData.transform.name);

            // Access components on the hit object (e.g., applying damage)
            // TargetHealth target = hitData.transform.GetComponent<TargetHealth>();
            // if (target != null)
            // {
            //     target.TakeDamage(gunDamage);
            // }
        }

        return true;
    }
}
