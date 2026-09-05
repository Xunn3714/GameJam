using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class SheepFlockAgent : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float maximumSpeed = 5.2f;
    [SerializeField, Min(0f)] private float acceleration = 14f;
    [SerializeField, Min(0f)] private float idleBraking = 18f;

    [Header("Flock Shape")]
    [SerializeField, Min(0f)] private float comfortableRadius = 1.65f;
    [SerializeField, Min(0f)] private float separationRadius = 1.15f;
    [SerializeField, Min(0f)] private float cohesionWeight = 2.2f;
    [SerializeField, Min(0f)] private float separationWeight = 4f;
    [SerializeField, Min(0f)] private float alignmentWeight = 0.35f;

    [Header("Organic Wander")]
    [SerializeField, Min(0f)] private float wanderStrength = 0.7f;
    [SerializeField, Min(0f)] private float wanderTurnSpeed = 1.6f;
    [SerializeField, Min(0.1f)] private float minimumWanderDuration = 0.55f;
    [SerializeField, Min(0.1f)] private float maximumWanderDuration = 1.25f;

    [Header("Settling")]
    [SerializeField, Min(0f)] private float idleCorrectionMargin = 0.18f;
    [SerializeField, Min(0f)] private float stopSpeed = 0.025f;

    [Header("Blocking")]
    [SerializeField] private LayerMask blockingLayers;
    [SerializeField, Min(0f)] private float blockingRadius = 0f;

    private Rigidbody2D body;
    private FlockController flock;
    private Vector2 velocity;
    private Vector2 wanderDirection;
    private Vector2 targetWanderDirection;
    private Vector2 fallbackSeparationDirection;
    private float wanderTimer;

    public Vector2 Velocity => velocity;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        ResetWander();
        if (blockingLayers.value == 0)
            blockingLayers = MovementBlocking.DefaultMask();
        if (blockingRadius <= 0f)
        {
            CircleCollider2D circle = GetComponent<CircleCollider2D>();
            blockingRadius = circle != null ? circle.radius : 0.3f;
        }
    }

    public void SetFlock(FlockController owner)
    {
        flock = owner;
        velocity = Vector2.zero;
        ResetWander();
        enabled = flock != null;
    }

    private void FixedUpdate()
    {
        if (flock == null || body == null || Time.timeScale == 0f)
            return;

        float deltaTime = Time.fixedDeltaTime;
        bool flockIsMoving = flock.IsMoving;
        Vector2 steeringVelocity = CalculateSteeringVelocity(flockIsMoving, deltaTime);
        float response = flockIsMoving ? acceleration : idleBraking;
        velocity = Vector2.MoveTowards(velocity, steeringVelocity, response * deltaTime);

        if (!flockIsMoving
            && steeringVelocity.sqrMagnitude <= stopSpeed * stopSpeed
            && velocity.sqrMagnitude <= stopSpeed * stopSpeed)
        {
            velocity = Vector2.zero;
            return;
        }

        Vector2 from = body.position;
        Vector2 target = MovementBlocking.ResolveMove(
            from,
            from + velocity * deltaTime,
            blockingRadius,
            blockingLayers);

        // 把实际走出去的位移反算回速度，避免贴墙的羊把"想走但没走成"的速度
        // 通过 alignment 传染给邻居，导致整群往墙里挤。
        velocity = (target - from) / deltaTime;

        if (target == from)
            return;

        body.MovePosition(target);
    }

    private Vector2 CalculateSteeringVelocity(bool flockIsMoving, float deltaTime)
    {
        Vector2 position = body.position;
        Vector2 desiredVelocity = flock.MovementVelocity;
        Vector2 centerOffset = flock.Center - position;
        float centerDistance = centerOffset.magnitude;

        float activeRadius = flockIsMoving
            ? comfortableRadius
            : comfortableRadius + idleCorrectionMargin;
        if (centerDistance > activeRadius)
        {
            float outsideDistance = centerDistance - activeRadius;
            desiredVelocity += centerOffset.normalized
                * Mathf.Min(outsideDistance * cohesionWeight, maximumSpeed);
        }

        Vector2 separation = Vector2.zero;
        Vector2 averageNeighborVelocity = Vector2.zero;
        int velocityNeighborCount = 0;
        IReadOnlyList<SheepMember> members = flock.Members;
        float separationRadiusSquared = separationRadius * separationRadius;

        for (int index = 0; index < members.Count; index++)
        {
            SheepMember member = members[index];
            if (member == null || member.gameObject == gameObject)
                continue;

            SheepFlockAgent neighbor = member.Agent;
            if (neighbor != null)
            {
                averageNeighborVelocity += neighbor.Velocity;
                velocityNeighborCount++;
            }

            Vector2 away = position - (Vector2)member.transform.position;
            float squaredDistance = away.sqrMagnitude;
            if (squaredDistance >= separationRadiusSquared)
                continue;

            if (squaredDistance <= 0.0001f)
            {
                separation += fallbackSeparationDirection;
                continue;
            }

            float distance = Mathf.Sqrt(squaredDistance);
            float closeness = 1f - distance / separationRadius;
            float avoidance = closeness + closeness * closeness;
            separation += away / distance * avoidance;
        }

        desiredVelocity += separation * separationWeight;

        if (flockIsMoving)
        {
            if (velocityNeighborCount > 0)
            {
                Vector2 averageVelocity = averageNeighborVelocity / velocityNeighborCount;
                desiredVelocity += (averageVelocity - velocity) * alignmentWeight;
            }

            UpdateWander(deltaTime);
            desiredVelocity += wanderDirection * wanderStrength;
        }
        else
        {
            wanderDirection = Vector2.MoveTowards(
                wanderDirection,
                Vector2.zero,
                wanderTurnSpeed * deltaTime);
        }

        return Vector2.ClampMagnitude(desiredVelocity, maximumSpeed);
    }

    private void UpdateWander(float deltaTime)
    {
        wanderTimer -= deltaTime;
        if (wanderTimer <= 0f)
        {
            targetWanderDirection = RandomDirection();
            wanderTimer = Random.Range(minimumWanderDuration, maximumWanderDuration);
        }

        wanderDirection = Vector2.MoveTowards(
            wanderDirection,
            targetWanderDirection,
            wanderTurnSpeed * deltaTime);
    }

    private void ResetWander()
    {
        fallbackSeparationDirection = RandomDirection();
        wanderDirection = RandomDirection();
        targetWanderDirection = RandomDirection();
        wanderTimer = Random.Range(minimumWanderDuration, maximumWanderDuration);
    }

    private static Vector2 RandomDirection()
    {
        Vector2 direction = Random.insideUnitCircle;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
    }

    private void OnValidate()
    {
        maximumWanderDuration = Mathf.Max(maximumWanderDuration, minimumWanderDuration);
        comfortableRadius = Mathf.Max(comfortableRadius, separationRadius);
    }
}
