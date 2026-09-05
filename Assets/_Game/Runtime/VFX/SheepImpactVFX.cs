using System.Collections;
using UnityEngine;

public class SheepImpactVFX : MonoBehaviour
{
    [Header("Impact Particle")]
    [SerializeField]
    private ParticleSystem impactVfxPrefab;

    [Header("Obstacle")]
    [SerializeField]
    private LayerMask obstacleLayers;


    [Header("Particle Burst")]
    [SerializeField]
    private int particleCount = 12;

    [SerializeField]
    private float minParticleSpeed = 1.2f;

    [SerializeField]
    private float maxParticleSpeed = 2.5f;

    [Range(0f, 90f)]
    [SerializeField]
    private float spreadAngle = 75f;


    [Header("Collision")]
    [SerializeField]
    private float minimumImpactSpeed = 0.5f;

    [SerializeField]
    private float surfaceOffset = 0.08f;

    [SerializeField]
    private float impactCooldown = 0.12f;


    [Header("Hit Slow")]
    [Range(0.01f, 1f)]
    [SerializeField]
    private float hitSlowTimeScale = 0.25f;

    [SerializeField]
    private float hitSlowDuration = 0.08f;


    [Header("Camera Shake")]
    [SerializeField]
    private CameraFollow2D cameraFollow;

    [SerializeField]
    private float cameraShakeDuration = 0.12f;

    [SerializeField]
    private float cameraShakeStrength = 0.10f;


    [Header("VFX Lifetime")]
    [SerializeField]
    private float impactVfxDestroyDelay = 2f;


    private float nextImpactTime;


    // =========================
    // Global Hit Slow State
    //
    // 防止多个羊同时撞击时，
    // 多个 Coroutine 互相把 TimeScale 改乱。
    // =========================

    private static bool hitSlowActive;
    private static float hitSlowRestoreScale = 1f;
    private static float hitSlowAppliedScale = 1f;
    private static int hitSlowVersion;


    private void Awake()
    {
        if (cameraFollow == null)
        {
            cameraFollow =
                FindFirstObjectByType<CameraFollow2D>();
        }
    }


    private void OnCollisionEnter2D(
        Collision2D collision
    )
    {
        TryPlayImpact(collision);
    }


    private void TryPlayImpact(
        Collision2D collision
    )
    {
        // =========================
        // 必须有 VFX
        // =========================

        if (impactVfxPrefab == null)
            return;


        // =========================
        // Cooldown
        // =========================

        if (Time.unscaledTime < nextImpactTime)
            return;


        // =========================
        // 只检测指定障碍物 Layer
        // =========================

        int otherLayer =
            collision.gameObject.layer;


        if ((obstacleLayers.value &
             (1 << otherLayer)) == 0)
        {
            return;
        }


        // =========================
        // 撞击速度
        // =========================

        if (collision.relativeVelocity.magnitude
            < minimumImpactSpeed)
        {
            return;
        }


        if (collision.contactCount <= 0)
            return;


        ContactPoint2D contact =
            collision.GetContact(0);


        Vector2 hitPoint =
            contact.point;


        Vector2 normal =
            contact.normal;


        // =========================
        // 确保 Normal 指向羊这一侧
        // =========================

        Vector2 towardSheep =
            (Vector2)transform.position -
            hitPoint;


        if (Vector2.Dot(
                normal,
                towardSheep
            ) < 0f)
        {
            normal = -normal;
        }


        if (normal.sqrMagnitude < 0.001f)
        {
            normal = Vector2.up;
        }


        normal.Normalize();


        // =========================
        // 生成位置向障碍物外侧偏移
        // 防止粒子嵌入障碍物
        // =========================

        Vector2 spawnPosition =
            hitPoint +
            normal * surfaceOffset;


        PlayImpactParticles(
            spawnPosition,
            normal
        );


        // =========================
        // Camera Shake
        // =========================

        if (cameraFollow == null)
        {
            cameraFollow =
                FindFirstObjectByType<CameraFollow2D>();
        }


        if (cameraFollow != null)
        {
            cameraFollow.Shake(
                cameraShakeDuration,
                cameraShakeStrength
            );
        }


        // =========================
        // Hit Slow
        // =========================

        TriggerHitSlow();


        // =========================
        // Cooldown
        // =========================

        nextImpactTime =
            Time.unscaledTime +
            impactCooldown;
    }


    private void PlayImpactParticles(
        Vector2 spawnPosition,
        Vector2 outwardNormal
    )
    {
        ParticleSystem particles =
            Instantiate(
                impactVfxPrefab,
                spawnPosition,
                Quaternion.identity
            );


        float baseAngle =
            Mathf.Atan2(
                outwardNormal.y,
                outwardNormal.x
            )
            * Mathf.Rad2Deg;


        ParticleSystem.MainModule main =
            particles.main;


        for (int i = 0;
             i < particleCount;
             i++)
        {
            float randomAngle =
                Random.Range(
                    -spreadAngle,
                    spreadAngle
                );


            float finalAngle =
                baseAngle +
                randomAngle;


            Vector2 direction =
                new Vector2(
                    Mathf.Cos(
                        finalAngle *
                        Mathf.Deg2Rad
                    ),
                    Mathf.Sin(
                        finalAngle *
                        Mathf.Deg2Rad
                    )
                );


            float speed =
                Random.Range(
                    minParticleSpeed,
                    maxParticleSpeed
                );


            ParticleSystem.EmitParams emitParams =
                new ParticleSystem.EmitParams();


            // 如果 Particle System 设置的是 World，
            // EmitParams 的 Position 也必须使用世界坐标。
            //
            // 如果以后美术把它改成 Local，
            // 则从 Prefab 根节点原点发射。
            if (main.simulationSpace ==
                ParticleSystemSimulationSpace.World)
            {
                emitParams.position =
                    spawnPosition;
            }
            else
            {
                emitParams.position =
                    Vector3.zero;
            }


            emitParams.velocity =
                direction * speed;


            particles.Emit(
                emitParams,
                1
            );
        }


        particles.Play(true);


        Destroy(
            particles.gameObject,
            impactVfxDestroyDelay
        );
    }


    private void TriggerHitSlow()
    {
        if (hitSlowDuration <= 0f)
            return;


        // =========================
        // 第一次进入 Hit Slow 时
        // 保存原始 TimeScale
        // =========================

        if (!hitSlowActive)
        {
            hitSlowRestoreScale =
                Time.timeScale;

            hitSlowActive = true;
        }


        // 如果当前已经比本次目标更慢，
        // 不把游戏强行加速。
        hitSlowAppliedScale =
            Mathf.Min(
                Time.timeScale,
                hitSlowTimeScale
            );


        Time.timeScale =
            hitSlowAppliedScale;


        hitSlowVersion++;


        int version =
            hitSlowVersion;


        StartCoroutine(
            RestoreHitSlow(
                version
            )
        );
    }


    private IEnumerator RestoreHitSlow(
        int version
    )
    {
        yield return
            new WaitForSecondsRealtime(
                hitSlowDuration
            );


        // 后面又发生了一次撞击，
        // 让最新的一次负责恢复。
        if (version != hitSlowVersion)
            yield break;


        // 如果期间 Pause / Result 等系统
        // 已经主动改变了 TimeScale，
        // 不擅自覆盖它们。
        if (Mathf.Approximately(
                Time.timeScale,
                hitSlowAppliedScale
            ))
        {
            Time.timeScale =
                hitSlowRestoreScale;
        }


        hitSlowActive = false;
    }
}
