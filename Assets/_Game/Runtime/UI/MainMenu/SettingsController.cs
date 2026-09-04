using UnityEngine;
using UnityEngine.UI;

public class SettingsController : MonoBehaviour
{
    [Header("Volume Sliders")]
    public Slider bgmSlider;
    public Slider sfxSlider;


    private void OnEnable()
    {
        LoadSliderValues();
    }


    private void LoadSliderValues()
    {
        float bgmVolume = PlayerPrefs.GetFloat(
            AudioManager.BGM_VOLUME_KEY, 1f);

        float sfxVolume = PlayerPrefs.GetFloat(
            AudioManager.SFX_VOLUME_KEY, 1f);

        bgmSlider.SetValueWithoutNotify(bgmVolume);
        sfxSlider.SetValueWithoutNotify(sfxVolume);
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


    private void OnDisable()
    {
        PlayerPrefs.Save();
    }


    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }
}
