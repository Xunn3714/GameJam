using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public const string BGM_VOLUME_KEY = "BGMVolume";
    public const string SFX_VOLUME_KEY = "SFXVolume";

    [Header("Audio Sources")]
    public AudioSource bgmSource;
    public AudioSource sfxSource;


    private void Awake()
    {
        // 保证全游戏只有一个 AudioManager
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 读取之前保存的音量
        LoadVolumeSettings();
    }


    private void LoadVolumeSettings()
    {
        float bgmVolume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY, 1f);
        float sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 1f);

        SetBGMVolume(bgmVolume);
        SetSFXVolume(sfxVolume);
    }


    // 播放背景音乐
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


    // 停止背景音乐
    public void StopBGM()
    {
        bgmSource.Stop();
    }


    // 播放一次音效
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null)
            return;

        sfxSource.PlayOneShot(clip);
    }


    // 设置 BGM 音量
    public void SetBGMVolume(float volume)
    {
        bgmSource.volume = Mathf.Clamp01(volume);
    }


    // 设置 SFX 音量
    public void SetSFXVolume(float volume)
    {
        sfxSource.volume = Mathf.Clamp01(volume);
    }
}
