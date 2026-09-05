using UnityEngine;

[RequireComponent(typeof(BreakableObstacle))]
public class BreakableObstacleAudio : MonoBehaviour
{
    [Header("Break SFX")]
    [SerializeField] private AudioClip breakClip;
    [SerializeField, Range(0f, 1f)] private float volumeScale = 0.6f;

    private BreakableObstacle breakableObstacle;

    private void Awake()
    {
        // Use the existing BreakableObstacle on this GameObject.
        breakableObstacle = GetComponent<BreakableObstacle>();
    }

    private void OnEnable()
    {
        // Listen for the obstacle's existing break event.
        if (breakableObstacle != null)
        {
            breakableObstacle.Broken += HandleBroken;
        }
    }

    private void OnDisable()
    {
        // Remove the event listener when this component is disabled.
        if (breakableObstacle != null)
        {
            breakableObstacle.Broken -= HandleBroken;
        }
    }

    private void HandleBroken(BreakableObstacle brokenObstacle)
    {
        // Only respond to the BreakableObstacle attached to this GameObject.
        if (brokenObstacle != breakableObstacle)
            return;

        // Play the break sound through the project's existing audio system.
        if (breakClip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(breakClip, volumeScale);
        }
    }
}