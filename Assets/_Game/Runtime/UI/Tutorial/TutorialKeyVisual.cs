using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>让草地教程键帽跟随真实键盘输入切换常态/按下态。</summary>
[DisallowMultipleComponent]
public sealed class TutorialKeyVisual : MonoBehaviour
{
    public enum TutorialKey
    {
        W,
        A,
        S,
        D,
        E,
        Space,
    }

    [SerializeField] private SpriteRenderer target;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite pressedSprite;
    [SerializeField] private TutorialKey key;

    public Sprite NormalSprite => normalSprite;
    public Sprite PressedSprite => pressedSprite;
    public TutorialKey Key => key;

    public void Configure(
        SpriteRenderer spriteRenderer,
        Sprite normal,
        Sprite pressed,
        TutorialKey tutorialKey)
    {
        target = spriteRenderer;
        normalSprite = normal;
        pressedSprite = pressed;
        key = tutorialKey;
        Refresh(false);
    }

    private void Awake()
    {
        if (target == null)
            target = GetComponent<SpriteRenderer>();
    }

    private void OnEnable() => Refresh(IsPressed());

    private void Update() => Refresh(IsPressed());

    private bool IsPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        return key switch
        {
            TutorialKey.W => keyboard.wKey.isPressed,
            TutorialKey.A => keyboard.aKey.isPressed,
            TutorialKey.S => keyboard.sKey.isPressed,
            TutorialKey.D => keyboard.dKey.isPressed,
            TutorialKey.E => keyboard.eKey.isPressed,
            TutorialKey.Space => keyboard.spaceKey.isPressed,
            _ => false,
        };
    }

    private void Refresh(bool pressed)
    {
        if (target == null)
            return;

        Sprite next = pressed && pressedSprite != null ? pressedSprite : normalSprite;
        if (next != null && target.sprite != next)
            target.sprite = next;
    }
}
