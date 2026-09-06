using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 真结局的演出：整群羊跳两下，宝塔没了，脚底下踩出一个洞，第二跳把洞换成更深的那张。
/// 每次落地都有屏幕震动 + 闪白。演完回调交给关卡控制器去结算。
/// </summary>
[DisallowMultipleComponent]
public sealed class TrueEndingSequence : MonoBehaviour
{
    [Header("Art")]
    [SerializeField] private Sprite holeFirstJumpSprite;
    [SerializeField] private Sprite holeSecondJumpSprite;

    [Header("Jump")]
    [SerializeField, Min(0.1f)] private float jumpHeight = 2.2f;
    [SerializeField, Min(0.1f)] private float jumpDuration = 0.6f;
    [SerializeField, Min(0f)] private float betweenJumpsDelay = 0.45f;
    [SerializeField, Min(0f)] private float finishDelay = 1.2f;

    [Header("Feedback")]
    [SerializeField, Min(0f)] private float shakeAmplitude = 0.9f;
    [SerializeField, Min(0f)] private float shakeDuration = 0.6f;
    [SerializeField, Min(0.05f)] private float flashDuration = 0.4f;

    [Header("Hole")]
    [Tooltip("洞的宽度 = 羊群半径 × 2 × 这个倍率，所以羊越多洞越大。")]
    [SerializeField, Min(0.1f)] private float holeWidthPerFlockDiameter = 1.45f;
    [SerializeField, Min(1f)] private float minimumHoleWidth = 6f;

    private FlockController flock;
    private CameraFollow2D cameraFollow;
    private ScreenFlashView flashView;
    private bool playing;

    public bool Playing => playing;

    public void Configure(FlockController flockController, CameraFollow2D camera,
        ScreenFlashView flash, Sprite firstJumpHole, Sprite secondJumpHole)
    {
        flock = flockController;
        cameraFollow = camera;
        flashView = flash;
        if (firstJumpHole != null) holeFirstJumpSprite = firstJumpHole;
        if (secondJumpHole != null) holeSecondJumpSprite = secondJumpHole;
    }

    /// <summary>演出真结局；演完调用 onComplete。pagoda 会在第一跳落地时消失。</summary>
    public void Play(GameObject pagoda, Action onComplete)
    {
        if (playing)
            return;

        playing = true;
        StartCoroutine(Run(pagoda, onComplete));
    }

    private IEnumerator Run(GameObject pagoda, Action onComplete)
    {
        Vector2 center = flock != null ? flock.Center : (Vector2)transform.position;

        // ---------------- 第一跳：宝塔消失，脚底下出现第一个洞 ----------------
        JumpAll();
        Punch();
        yield return new WaitForSecondsRealtime(jumpDuration * 0.5f);

        if (pagoda != null)
            Destroy(pagoda);

        SpriteRenderer hole = CreateHole(center);
        yield return new WaitForSecondsRealtime(jumpDuration * 0.5f + betweenJumpsDelay);

        // ---------------- 第二跳：洞换成更深的那张 ----------------
        JumpAll();
        Punch();
        yield return new WaitForSecondsRealtime(jumpDuration * 0.5f);

        if (hole != null && holeSecondJumpSprite != null)
            hole.sprite = holeSecondJumpSprite;

        yield return new WaitForSecondsRealtime(jumpDuration * 0.5f + finishDelay);

        playing = false;
        onComplete?.Invoke();
    }

    private void JumpAll()
    {
        if (flock == null)
            return;

        for (int index = 0; index < flock.Members.Count; index++)
        {
            SheepMember member = flock.Members[index];
            SheepVisualAnimator animator = member != null ? member.GetComponent<SheepVisualAnimator>() : null;
            animator?.PlayJump(jumpHeight, jumpDuration);
        }
    }

    private void Punch()
    {
        cameraFollow?.Shake(shakeAmplitude, shakeDuration);
        flashView?.Flash(flashDuration);
    }

    /// <summary>洞按羊群大小等比例放大：宽度跟着羊群直径走，高度按贴图比例走。</summary>
    private SpriteRenderer CreateHole(Vector2 center)
    {
        if (holeFirstJumpSprite == null)
            return null;

        float diameter = flock != null ? flock.CurrentMembershipRadius * 2f : minimumHoleWidth;
        float width = Mathf.Max(minimumHoleWidth, diameter * holeWidthPerFlockDiameter);
        float spriteWidth = Mathf.Max(0.001f, holeFirstJumpSprite.bounds.size.x);

        GameObject result = new GameObject("TrueEndingHole");
        result.transform.position = center;
        result.transform.localScale = Vector3.one * (width / spriteWidth);

        SpriteRenderer renderer = result.AddComponent<SpriteRenderer>();
        renderer.sprite = holeFirstJumpSprite;
        // 沉到背景层，羊踩在洞上面。
        renderer.sortingLayerName = "Background";
        renderer.sortingOrder = 20;
        return renderer;
    }
}
