using UnityEngine;
using UnityEngine.UI;

namespace GameJam.Game.UI
{
    /// <summary>
    /// Drives a menu background. A repeating Texture can scroll through the RawImage; a Sprite is
    /// gently panned instead. Both modes inherit the offset of an optional CameraShake2D.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuBackgroundMotion : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform motionRoot;
        [SerializeField] private RawImage scrollingTextureImage;
        [SerializeField] private Image spriteImage;
        [SerializeField] private CameraShake2D cameraShake;

        [Header("Background Source")]
        [SerializeField] private Texture backgroundTexture;
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private bool scrollTexture = true;

        [Header("Motion")]
        [Tooltip("UV movement per second. The texture import setting must use Wrap Mode = Repeat.")]
        [SerializeField] private Vector2 textureScrollSpeed = new Vector2(0.012f, 0.005f);
        [Tooltip("The gentle pan applied when a Sprite is used as the background.")]
        [SerializeField] private Vector2 spritePanDistance = new Vector2(28f, 16f);
        [SerializeField] private Vector2 spritePanFrequency = new Vector2(0.11f, 0.16f);
        [SerializeField, Min(0f)] private float shakePixelsPerWorldUnit = 80f;
        [SerializeField] private bool useUnscaledTime = true;

        private Vector2 initialPosition;
        private Rect initialUvRect;
        private float elapsedTime;

        private void Awake()
        {
            CacheInitialState();
            ApplyBackground();
        }

        private void OnEnable()
        {
            CacheInitialState();
        }

        private void OnValidate()
        {
            ApplyBackground();
        }

        private void Update()
        {
            var deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsedTime += deltaTime;

            UpdateTextureScroll();
            UpdateVisualOffset();
        }

        /// <summary>Assign a tiled/repeating texture background and enable its UV scroll.</summary>
        public void SetBackground(Texture texture)
        {
            backgroundTexture = texture;
            backgroundSprite = null;
            ApplyBackground();
        }

        /// <summary>Assign a sprite background. It receives a subtle panoramic motion.</summary>
        public void SetBackground(Sprite sprite)
        {
            backgroundSprite = sprite;
            backgroundTexture = null;
            ApplyBackground();
        }

        public void Configure(
            RectTransform root,
            RawImage textureImage,
            Image image,
            CameraShake2D shake)
        {
            motionRoot = root;
            scrollingTextureImage = textureImage;
            spriteImage = image;
            cameraShake = shake;
            CacheInitialState();
            ApplyBackground();
        }

        private void CacheInitialState()
        {
            if (motionRoot != null)
            {
                initialPosition = motionRoot.anchoredPosition;
            }

            if (scrollingTextureImage != null)
            {
                initialUvRect = scrollingTextureImage.uvRect;
            }
        }

        private void ApplyBackground()
        {
            if (scrollingTextureImage != null)
            {
                var hasTexture = backgroundTexture != null;
                scrollingTextureImage.enabled = hasTexture || backgroundSprite == null;
                if (hasTexture)
                {
                    scrollingTextureImage.texture = backgroundTexture;
                }
            }

            if (spriteImage != null)
            {
                spriteImage.sprite = backgroundSprite;
                spriteImage.enabled = backgroundSprite != null;
            }
        }

        private void UpdateTextureScroll()
        {
            if (scrollingTextureImage == null || !scrollingTextureImage.enabled ||
                scrollingTextureImage.texture == null || !scrollTexture)
            {
                return;
            }

            var uvRect = initialUvRect;
            uvRect.position += textureScrollSpeed * elapsedTime;
            scrollingTextureImage.uvRect = uvRect;
        }

        private void UpdateVisualOffset()
        {
            if (motionRoot == null)
            {
                return;
            }

            var spritePan = backgroundSprite == null
                ? Vector2.zero
                : new Vector2(
                    Mathf.Sin(elapsedTime * spritePanFrequency.x * Mathf.PI * 2f) * spritePanDistance.x,
                    Mathf.Cos(elapsedTime * spritePanFrequency.y * Mathf.PI * 2f) * spritePanDistance.y);
            var shakeOffset = cameraShake == null
                ? Vector2.zero
                : (Vector2)cameraShake.CurrentOffset * shakePixelsPerWorldUnit;

            motionRoot.anchoredPosition = initialPosition + spritePan + shakeOffset;
        }
    }
}
