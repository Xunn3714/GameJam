using UnityEngine;
using UnityEngine.InputSystem;

public class SfxTest : MonoBehaviour
{
    private AudioSource sfxSource;

    void Awake()
    {
        sfxSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            sfxSource.Play();
        }
    }
}