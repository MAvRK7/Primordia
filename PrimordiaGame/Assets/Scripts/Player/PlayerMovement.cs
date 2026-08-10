using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public Rigidbody rb;
    Camera cam;
    public float forwardforce = 1000f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
     cam = Camera.main;   
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey("w"))
        {
            rb.AddForce(0,0, forwardforce *Time.deltaTime);
        }
         if (Input.GetKey("s"))
        {
            rb.AddForce(0,0, -forwardforce *Time.deltaTime);
        }
        if (Input.GetKey("d"))
       {
            rb.AddForce(forwardforce* Time.deltaTime,0,0);
       }
        if (Input.GetKey("a"))
       {
            rb.AddForce(-forwardforce* Time.deltaTime,0,0);
    }
    }
}
