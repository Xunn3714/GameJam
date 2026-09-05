using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public sealed class SheepMember : MonoBehaviour
{
    public FlockController Flock { get; private set; }
    public SheepFlockAgent Agent { get; private set; }

    private void Awake()
    {
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.useFullKinematicContacts = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        GetComponent<CircleCollider2D>().isTrigger = true;
        SheepVisualAnimator.Ensure(gameObject);
    }

    internal bool Join(FlockController flock)
    {
        if (flock == null || (Flock != null && Flock != flock))
            return false;

        Flock = flock;
        return true;
    }

    internal void SetAgent(SheepFlockAgent agent)
    {
        Agent = agent;
    }

    internal void Leave(FlockController flock)
    {
        if (Flock == flock)
        {
            Flock = null;
            Agent = null;
        }
    }
}
