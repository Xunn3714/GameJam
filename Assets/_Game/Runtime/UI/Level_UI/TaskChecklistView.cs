using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TaskChecklistView : MonoBehaviour
{
    [Header("Check Icons")]
    [SerializeField] private Image firstTaskIcon;
    [SerializeField] private Image secondTaskIcon;

    [Header("Sprites")]
    [SerializeField] private Sprite uncheckedSprite;
    [SerializeField] private Sprite checkedSprite;

    private FlockController flockController;


    private void OnEnable()
    {
        Refresh(0);
        StartCoroutine(FindFlockController());
    }


    private IEnumerator FindFlockController()
    {
        while (flockController == null)
        {
            flockController =
                FindFirstObjectByType<FlockController>();

            if (flockController == null)
            {
                yield return null;
            }
        }

        flockController.SheepRecruited += OnSheepRecruited;

        Refresh(flockController.RecruitedCount);
    }


    private void OnDisable()
    {
        StopAllCoroutines();

        if (flockController != null)
        {
            flockController.SheepRecruited -= OnSheepRecruited;
        }
    }


    private void OnSheepRecruited(
        RecruitableSheep sheep,
        int recruitedCount)
    {
        Refresh(recruitedCount);
    }


    private void Refresh(int recruitedCount)
    {
        if (firstTaskIcon != null)
        {
            firstTaskIcon.sprite =
                recruitedCount >= 1
                    ? checkedSprite
                    : uncheckedSprite;
        }

        if (secondTaskIcon != null)
        {
            secondTaskIcon.sprite =
                recruitedCount >= 2
                    ? checkedSprite
                    : uncheckedSprite;
        }
    }
}
