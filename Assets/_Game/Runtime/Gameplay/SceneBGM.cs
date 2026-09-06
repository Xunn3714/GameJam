using UnityEngine;

[DisallowMultipleComponent]
public sealed class SceneBGM : MonoBehaviour
{
    [Header("BGM")]
    [SerializeField] private AudioClip bgmClip;

    public void Configure(AudioClip clip)
    {
        bgmClip = clip;
    }

    private void Start()
    {
        if (bgmClip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayBGM(bgmClip);
    }
}
