using UnityEngine;

public class PlayerNoise : MonoBehaviour
{
    public float moveNoise = 12f;   // how far the sound reaches while moving

    // Shared "drop-box" the dino can read directly
    public static Vector3 lastNoisePos;
    public static float   lastNoiseRange;
    public static float   lastNoiseTime = -999f;   // start long ago = "no recent noise"

    void Update()
    {
        // Am I moving? (any WASD held)
        bool moving = Input.GetKey("w") || Input.GetKey("a")
                   || Input.GetKey("s") || Input.GetKey("d");

        if (moving)
        {
            lastNoisePos   = transform.position;  // where the noise happened
            lastNoiseRange = moveNoise;           // how far it carries
            lastNoiseTime  = Time.time;           // when it happened
        }
    }
}