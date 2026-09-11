using System;
using UnityEngine;

/// <summary>
/// 运羊卡车：沿公路从一头开到另一头后消失。
/// 撞到在群成员 → 像被狼撞散一样脱队（可重新收编）；撞到已经脱队的散羊 → 直接抓走（不可恢复）。
/// 羊数减到 0 由关卡控制器的现有失败逻辑处理。
/// </summary>
[DisallowMultipleComponent]
public sealed class TruckHazard : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float speed = 16f;
    [SerializeField, Min(0f)] private float knockbackSpeed = 9f;
    [SerializeField] private Color carriedSheepColor = new Color(0.9f, 0.75f, 0.75f, 1f);

    private Vector2 target;
    private Vector2 direction = Vector2.right;
    private Rigidbody2D body;
    private int carriedCount;

    /// <summary>卡车抓走一只羊时触发，供关卡统计。</summary>
    /// <summary>车身长度（世界单位）。大运.png 有效像素约占贴图高度的 82%，8 单位贴图 ≈ 6.5 单位可见车身。</summary>
    private const float TruckLength = 8f;

    public static event Action<SheepMember> SheepTaken;

    public static TruckHazard Spawn(Vector2 from, Vector2 to, float laneWidth, Sprite sprite, float speed)
    {
        GameObject truckObject = new GameObject("SheepTruck");
        truckObject.transform.position = from;
        Vector2 direction = (to - from).normalized;
        float heading = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 占位贴图车头朝 +X；美术图（大运.png）是竖着画的俯视车，车头朝 -Y，需要多转 90°。
        Vector2 spriteSize = sprite != null ? (Vector2)sprite.bounds.size : new Vector2(4f, 2f);
        bool portrait = spriteSize.y > spriteSize.x;
        truckObject.transform.rotation = Quaternion.Euler(0f, 0f, portrait ? heading + 90f : heading);

        SpriteRenderer renderer = truckObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite != null ? sprite : RuntimeSprites.Solid();
        if (sprite == null)
        {
            renderer.color = new Color(0.55f, 0.35f, 0.2f);
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = spriteSize;
        }
        renderer.sortingOrder = 30;
        // 车身长约 TruckLength 单位、宽不超过路面的 0.7。
        float length = portrait ? spriteSize.y : spriteSize.x;
        float scale = TruckLength / Mathf.Max(0.1f, length);
        truckObject.transform.localScale = Vector3.one * scale;

        Rigidbody2D truckBody = truckObject.AddComponent<Rigidbody2D>();
        truckBody.bodyType = RigidbodyType2D.Kinematic;
        truckBody.gravityScale = 0f;
        truckBody.useFullKinematicContacts = true;

        BoxCollider2D trigger = truckObject.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        float laneLimit = laneWidth * 0.7f / scale;
        trigger.size = portrait
            ? new Vector2(Mathf.Min(spriteSize.x, laneLimit), spriteSize.y)
            : new Vector2(spriteSize.x, Mathf.Min(spriteSize.y, laneLimit));

        TruckHazard truck = truckObject.AddComponent<TruckHazard>();
        truck.target = to;
        truck.direction = direction;
        truck.speed = speed;
        return truck;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        if (Time.timeScale == 0f)
            return;

        Vector2 next = Vector2.MoveTowards(body.position, target, speed * Time.fixedDeltaTime);
        body.MovePosition(next);
        if ((next - target).sqrMagnitude < 0.01f)
            Destroy(gameObject);   // 被抓走的羊挂在车下，一起消失。
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        SheepMember member = other.GetComponentInParent<SheepMember>();
        if (member == null || member.transform.IsChildOf(transform))
            return;

        if (member.Flock != null)
        {
            // 在群成员：往车身侧向撞开，进入散羊状态。
            Vector2 side = new Vector2(-direction.y, direction.x);
            float sign = Vector2.Dot((Vector2)member.transform.position - body.position, side) >= 0f ? 1f : -1f;
            ScatteredSheep.Scatter(member, (side * sign + direction * 0.3f).normalized * knockbackSpeed);
            return;
        }

        ScatteredSheep scattered = member.GetComponent<ScatteredSheep>();
        if (scattered == null || !scattered.IsScattered)
            return;

        Carry(member);
        SheepTaken?.Invoke(member);
    }

    /// <summary>和狼叼走一样：移出物理、挂到车身下面。</summary>
    private void Carry(SheepMember sheep)
    {
        ScatteredSheep scattered = sheep.GetComponent<ScatteredSheep>();
        if (scattered != null)
            scattered.enabled = false;
        foreach (Collider2D collider in sheep.GetComponents<Collider2D>())
            collider.enabled = false;
        Rigidbody2D sheepBody = sheep.GetComponent<Rigidbody2D>();
        if (sheepBody != null)
            sheepBody.simulated = false;
        SpriteRenderer sheepRenderer = sheep.GetComponent<SpriteRenderer>();
        if (sheepRenderer != null)
        {
            sheepRenderer.color = carriedSheepColor;
            sheepRenderer.sortingOrder = 31;
        }

        Vector3 worldScale = sheep.transform.lossyScale;
        sheep.transform.SetParent(transform, false);
        sheep.transform.localScale = new Vector3(
            worldScale.x / Mathf.Max(transform.lossyScale.x, 0.0001f),
            worldScale.y / Mathf.Max(transform.lossyScale.y, 0.0001f),
            1f);
        carriedCount++;
        sheep.transform.localPosition = new Vector3(-0.35f * carriedCount, 0.2f, 0f);
    }
}
