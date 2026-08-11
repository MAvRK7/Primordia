using UnityEngine;

public class Ranged : Item {
    public float damage;
    public Ammo ammo;
    public Transform target;
    public Transform firePoint;

    public bool fire() {
        
        GameObject fired = Instantiate(ammo.gameObject, firePoint.position, firePoint.rotation);
        
        return true;
    }
}
