using UnityEngine;

public class SceneBGM : MonoBehaviour
{
    [Header("BGM")]
    [SerializeField] private AudioClip bgmClip;

    private void Start()
    {
        // Play this scene's BGM through the existing AudioManager.
        if (bgmClip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(bgmClip);
        }
    }
}