using UnityEngine;

// Plays a footstep sound timed to actual movement: one step per
// profile.footstepInterval metres travelled. Faster dinos → faster steps,
// automatically, because it measures distance, not time.
//
// Additive & silent-safe: DinoAI auto-adds this component. Null clip, no
// AudioSource, or no profile → does nothing, no errors.
public class DinoFootsteps : MonoBehaviour
{
    private DinoProfile profile;
    private AudioSource audioSource;   // optional — shared with the rest of the dino audio
    private Vector3 lastPos;
    private float distanceAccum;

    // Called by DinoAI.Start.
    public void Init(DinoProfile prof, AudioSource src)
    {
        profile = prof;
        audioSource = src;
        lastPos = transform.position;
        distanceAccum = 0f;
    }

    void Start()
    {
        // In case Init wasn't called (component added manually), self-resolve.
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (profile == null)
        {
            DinoAI ai = GetComponent<DinoAI>();
            if (ai != null) profile = ai.profile;
        }
        lastPos = transform.position;
    }

    void Update()
    {
        if (profile == null) return;

        // horizontal distance travelled since last frame
        Vector3 delta = transform.position - lastPos;
        delta.y = 0f;
        float moved = delta.magnitude;
        lastPos = transform.position;

        // ignore teleports / navmesh warps so we don't machine-gun footsteps
        if (moved > 5f) return;

        distanceAccum += moved;

        float interval = profile.footstepInterval > 0f ? profile.footstepInterval : 2f;
        if (distanceAccum >= interval)
        {
            distanceAccum -= interval;
            PlayStep();
        }
    }

    void PlayStep()
    {
        if (profile.footstepSound == null || audioSource == null) return;
        // tiny pitch variation so repeated steps don't sound robotic
        float prevPitch = audioSource.pitch;
        audioSource.pitch = Random.Range(0.92f, 1.08f);
        audioSource.PlayOneShot(profile.footstepSound);
        audioSource.pitch = prevPitch;
    }
}
