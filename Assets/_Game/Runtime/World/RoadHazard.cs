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
        // 随机从哪头开过来，卡车横跨整条路。
        bool forward = Random.value < 0.5f;
        Vector2 from = forward ? start : end;
        Vector2 to = forward ? end : start;
        Vector2 axis = (to - from).normalized;
        // 起点在路外一点，让车"开进来"。
        activeTruck = TruckHazard.Spawn(
            from - axis * 6f,
            to + axis * 6f,
            width,
            truckSprite != null ? truckSprite : RuntimeSprites.Truck(),
            truckSpeed);
    }
}
