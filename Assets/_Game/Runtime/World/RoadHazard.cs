using UnityEngine;

/// <summary>
/// 区块交界处的公路：可以正常通过，但羊群一踩上来就会有一辆运羊卡车沿路开过。
/// 冷却期间和已有卡车在路上时不重复触发。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class RoadHazard : MonoBehaviour
{
    [SerializeField] private Vector2 start;
    [SerializeField] private Vector2 end;
    [SerializeField, Min(0.5f)] private float width = 6f;
    [SerializeField] private Sprite truckSprite;
    [SerializeField, Min(0.1f)] private float truckSpeed = 16f;
    [SerializeField, Min(0f)] private float cooldown = 8f;
    [Tooltip("卡车从踩上公路的那只羊沿路往回多少单位处出现（刚好在屏幕外），几乎是踩上去的一瞬间就撞过来。")]
    [SerializeField, Min(0f)] private float truckLeadDistance = 14f;

    private TruckHazard activeTruck;
    private float nextTruckTime;

    public void Configure(Vector2 roadStart, Vector2 roadEnd, float roadWidth, Sprite sprite, float speed, float cooldownSeconds)
    {
        start = roadStart;
        end = roadEnd;
        width = roadWidth;
        truckSprite = sprite;
        truckSpeed = speed;
        cooldown = cooldownSeconds;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Time.timeScale == 0f || activeTruck != null || Time.time < nextTruckTime)
            return;
        if (!BreakableObstacle.IsFlockContact(other))
            return;

        nextTruckTime = Time.time + cooldown;
        // 随机从哪头开过来，但不是从路的尽头出发：把踩上来的那只羊投影到路中线，
        // 往回 truckLeadDistance 处生成，然后一路开到另一头。
        Vector2 axis = (end - start).normalized;
        float along = Mathf.Clamp(Vector2.Dot((Vector2)other.transform.position - start, axis), 0f, (end - start).magnitude);
        Vector2 onRoad = start + axis * along;
        bool forward = Random.value < 0.5f;
        Vector2 heading = forward ? axis : -axis;
        Vector2 to = (forward ? end : start) + heading * 6f;
        activeTruck = TruckHazard.Spawn(
            onRoad - heading * truckLeadDistance,
            to,
            width,
            truckSprite,
            truckSpeed);
    }
}
