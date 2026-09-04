using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public const string BGM_VOLUME_KEY = "BGMVolume";
    public const string SFX_VOLUME_KEY = "SFXVolume";
    public const string SHEEP_VOLUME_KEY = "SheepVolume";

    [Header("Audio Sources")]
    public AudioSource bgmSource;
    public AudioSource sfxSource;
    public AudioSource sheepSource;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadVolumeSettings();
    }


    private void LoadVolumeSettings()
    {
        float bgmVolume =
            PlayerPrefs.GetFloat(BGM_VOLUME_KEY, 1f);

        float sfxVolume =
            PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 1f);

        float sheepVolume =
            PlayerPrefs.GetFloat(SHEEP_VOLUME_KEY, 1f);

        SetBGMVolume(bgmVolume);
        SetSFXVolume(sfxVolume);
        SetSheepVolume(sheepVolume);
    }


    // 播放 BGM
    public void PlayBGM(AudioClip clip)
    {
        if (clip == null)
            return;

        if (bgmSource.clip == clip && bgmSource.isPlaying)
            return;

        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.Play();
    }


    // 停止 BGM
    public void StopBGM()
    {
        bgmSource.Stop();
    }


    // 播放普通 SFX
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null)
            return;

        sfxSource.PlayOneShot(clip);
    }


    // 播放 Sheep 音效
    public void PlaySheepSFX(AudioClip clip)
    {
        if (clip == null)
            return;

        sheepSource.PlayOneShot(clip);
    }


    public void SetBGMVolume(float volume)
    {
        bgmSource.volume = Mathf.Clamp01(volume);
    }


    public void SetSFXVolume(float volume)
    {
        sfxSource.volume = Mathf.Clamp01(volume);
    }


    public void SetSheepVolume(float volume)
    {
        sheepSource.volume = Mathf.Clamp01(volume);
    }
}
