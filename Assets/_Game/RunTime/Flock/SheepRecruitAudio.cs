using UnityEngine;

public sealed class SheepRecruitAudio : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TutorialPen tutorialPen;

    [Header("Recruit SFX")]
    [SerializeField] private AudioClip[] sheepClips;
    [SerializeField, Range(0f, 1f)] private float recruitBleatChance = 0.5f;

    public void Configure(
        TutorialPen pen,
        AudioClip[] clips,
        float bleatChance = 0.5f)
    {
        tutorialPen = pen;
        sheepClips = clips;
        recruitBleatChance = Mathf.Clamp01(bleatChance);
    }

    private void OnEnable()
    {
        RecruitableSheep.AnyRecruited += HandleSheepRecruited;
    }

    private void OnDisable()
    {
        RecruitableSheep.AnyRecruited -= HandleSheepRecruited;
    }

    private void HandleSheepRecruited(
        RecruitableSheep sheep,
        bool wasReturning)
    {
        // A sheep returning after being scattered always bleats.
        if (wasReturning)
        {
            PlayRandomBleat();
            return;
        }

        // No recruit bleats during the first tutorial stage.
        if (tutorialPen != null && !tutorialPen.IsOpen)
            return;

        // Normal recruitment only bleats with the configured probability.
        if (Random.value > recruitBleatChance)
            return;

        PlayRandomBleat();
    }

    private void PlayRandomBleat()
    {
        if (sheepClips == null ||
            sheepClips.Length == 0 ||
            AudioManager.Instance == null)
        {
            return;
        }

        int startIndex = Random.Range(0, sheepClips.Length);

        for (int i = 0; i < sheepClips.Length; i++)
        {
            AudioClip clip =
                sheepClips[(startIndex + i) % sheepClips.Length];

            if (clip != null)
            {
                AudioManager.Instance.PlaySheepSFX(clip);
                return;
            }
        }
    }
}
