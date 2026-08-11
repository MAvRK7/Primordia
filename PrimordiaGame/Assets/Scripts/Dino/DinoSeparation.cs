using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DinoSeparation : MonoBehaviour
{
    public float pushForce = 6f;

    void OnCollisionStay(Collision c)
    {
        if (c.transform.GetComponent<DinoAI>() == null) return;   // only push off other dinos

        Vector3 away = transform.position - c.transform.position;
        away.y = 0f;                                              // horizontal only, stay on navmesh
        if (away.sqrMagnitude < 0.0001f)
            away = new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f);

        away.Normalize();
        transform.position += away * pushForce * Time.deltaTime;
    }
}