using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public const string MASTER_VOLUME_KEY = "MasterVolume";
    public const string BGM_VOLUME_KEY = "BGMVolume";
    public const string SFX_VOLUME_KEY = "SFXVolume";
    public const string SHEEP_VOLUME_KEY = "SheepVolume";

    [Header("Audio Sources")]
    public AudioSource bgmSource;
    public AudioSource sfxSource;
    public AudioSource sheepSource;

    private float masterVolume = 1f;
    private float bgmVolume = 1f;
    private float sfxVolume = 1f;
    private float sheepVolume = 1f;
    private readonly Dictionary<AudioClip, float> nextSfxPlayTimes = new Dictionary<AudioClip, float>();


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
        masterVolume = PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, 1f);
        bgmVolume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY, 1f);
        sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 1f);
        sheepVolume = PlayerPrefs.GetFloat(SHEEP_VOLUME_KEY, 1f);
        ApplyVolumes();
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
    public void PlaySFX(AudioClip clip, float volumeScale = 1f, float minimumRepeatInterval = 0f)
    {
        if (clip == null || sfxSource == null)
            return;

        if (minimumRepeatInterval > 0f)
        {
            float now = Time.unscaledTime;
            if (nextSfxPlayTimes.TryGetValue(clip, out float nextPlayTime) && now < nextPlayTime)
                return;

            nextSfxPlayTimes[clip] = now + minimumRepeatInterval;
        }

        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }


    // 播放 Sheep 音效
    public void PlaySheepSFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || sheepSource == null)
            return;

        sheepSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }


    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
    }


    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
    }


    public void SetSheepVolume(float volume)
    {
        sheepVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
    }

    private void ApplyVolumes()
    {
        if (bgmSource != null)
            bgmSource.volume = masterVolume * bgmVolume;

        if (sfxSource != null)
            sfxSource.volume = masterVolume * sfxVolume;

        if (sheepSource != null)
            sheepSource.volume = masterVolume * sheepVolume;
    }
}
