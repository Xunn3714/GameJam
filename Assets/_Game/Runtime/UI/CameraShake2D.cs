using UnityEngine;

namespace GameJam.Game.UI
{
    /// <summary>
    /// Small, self-contained camera shake for 2D scenes. The current offset is also exposed so
    /// screen-space menu backgrounds can visually follow the shake.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraShake2D : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float defaultDuration = 0.18f;
        [SerializeField, Min(0f)] private float defaultMagnitude = 0.08f;
        [SerializeField] private bool useUnscaledTime = true;

        private Vector3 restLocalPosition;
        private float remainingTime;
        private float totalDuration;
        private float magnitude;

        public Vector3 CurrentOffset { get; private set; }

        private void OnEnable()
        {
            restLocalPosition = transform.localPosition;
            CurrentOffset = Vector3.zero;
        }

        private void OnDisable()
        {
            transform.localPosition = restLocalPosition;
            CurrentOffset = Vector3.zero;
            remainingTime = 0f;
        }

        private void LateUpdate()
        {
            if (remainingTime <= 0f)
            {
                CurrentOffset = Vector3.zero;
                return;
            }

            var deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            remainingTime = Mathf.Max(0f, remainingTime - deltaTime);
            var strength = magnitude * (remainingTime / totalDuration);
            var sample = Random.insideUnitCircle * strength;
            CurrentOffset = new Vector3(sample.x, sample.y, 0f);
            transform.localPosition = restLocalPosition + CurrentOffset;

            if (remainingTime <= 0f)
            {
                transform.localPosition = restLocalPosition;
                CurrentOffset = Vector3.zero;
            }
        }

        /// <summary>Starts a shake using the values configured in the Inspector.</summary>
        public void Shake()
        {
            Shake(defaultDuration, defaultMagnitude);
        }

        /// <summary>Starts or replaces the current shake.</summary>
        public void Shake(float duration, float shakeMagnitude)
        {
            if (duration <= 0f || shakeMagnitude <= 0f)
            {
                StopShake();
                return;
            }

            // Capture the camera's normal position immediately before the shake begins.
            if (remainingTime <= 0f)
            {
                restLocalPosition = transform.localPosition;
            }

            totalDuration = duration;
            remainingTime = duration;
            magnitude = shakeMagnitude;
        }

        public void StopShake()
        {
            remainingTime = 0f;
            CurrentOffset = Vector3.zero;
            transform.localPosition = restLocalPosition;
        }
    }
}
