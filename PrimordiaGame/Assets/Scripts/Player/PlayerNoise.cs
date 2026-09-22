using UnityEngine;

public class PlayerNoise : MonoBehaviour
{
    public float moveNoise = 12f;   // how far the sound reaches while moving

    Transform motionSource;
    Vector3 previousPosition;
    float nextMovementSample;

    void Start()
    {
        var playerCamera = GetComponentInChildren<Camera>(true);
        motionSource = playerCamera != null ? playerCamera.transform : transform;
        previousPosition = motionSource.position;
        nextMovementSample = Time.time + 0.25f;
    }

    // Shared "drop-box" the dino can read directly
    public static Vector3 lastNoisePos;
    public static float   lastNoiseRange;
    public static float   lastNoiseTime = -999f;   // start long ago = "no recent noise"

    public static void EmitNoise(Vector3 position, float range)
    {
        if (range <= 0f)
            return;

        lastNoisePos = position;
        lastNoiseRange = range;
        lastNoiseTime = Time.time;
    }

    void LateUpdate()
    {
        if (Time.time < nextMovementSample)
            return;

        // Observe actual motion so both thumbstick and room-scale movement work
        // without calling the disabled legacy keyboard input backend.
        var position = motionSource.position;
        var movement = Vector3.ProjectOnPlane(position - previousPosition, Vector3.up);
        if (movement.sqrMagnitude >= 0.01f)
            EmitNoise(position, moveNoise);
        previousPosition = position;
        nextMovementSample = Time.time + 0.25f;
    }
}
