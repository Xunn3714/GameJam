using UnityEngine;
using UnityEngine.UI;

public class SettingsController : MonoBehaviour
{
    [Header("Volume Sliders")]
    public Slider bgmSlider;
    public Slider sfxSlider;
    public Slider sheepSlider;


    private void OnEnable()
    {
        LoadSliderValues();
    }


    private void LoadSliderValues()
    {
        float bgmVolume =
            PlayerPrefs.GetFloat(
                AudioManager.BGM_VOLUME_KEY, 1f);

        float masterVolume =
            PlayerPrefs.GetFloat(
                AudioManager.MASTER_VOLUME_KEY, 1f);

        float sfxVolume =
            PlayerPrefs.GetFloat(
                AudioManager.SFX_VOLUME_KEY, 1f);

        if (bgmSlider != null)
            bgmSlider.SetValueWithoutNotify(bgmVolume);

        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(masterVolume);

        if (sheepSlider != null)
            sheepSlider.SetValueWithoutNotify(sfxVolume);
    }


    public void SetBGMVolume(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetBGMVolume(value);
        }

        PlayerPrefs.SetFloat(
            AudioManager.BGM_VOLUME_KEY, value);
    }


    public void SetSFXVolume(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(value);
        }

        PlayerPrefs.SetFloat(
            AudioManager.SFX_VOLUME_KEY, value);
    }

    public void SetMasterVolume(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMasterVolume(value);

        PlayerPrefs.SetFloat(AudioManager.MASTER_VOLUME_KEY, value);
    }


    public void SetSheepVolume(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(value);
        }

        PlayerPrefs.SetFloat(
            AudioManager.SFX_VOLUME_KEY, value);
    }


    private void OnDisable()
    {
        PlayerPrefs.Save();
    }


    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }
}
