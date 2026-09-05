using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SheepCardView : MonoBehaviour
{
    [Header("UI")]
    public Image sheepImage;
    public TMP_Text nameText;
    public TMP_Text countText;
    public Button button;

    private Action onClick;


    public void Setup(
        Sprite sprite,
        string sheepName,
        int encounterCount,
        bool unlocked,
        Action clickAction)
    {
        onClick = clickAction;

        // 图片
        if (sheepImage != null)
        {
            sheepImage.sprite = sprite;
            sheepImage.enabled = sprite != null;
        }

        // 已解锁
        if (unlocked)
        {
            nameText.text = sheepName;
            countText.text = $"遇到过 {encounterCount} 次";
        }
        // 未解锁
        else
        {
            nameText.text = "???";
            countText.text = "尚未解锁";
        }

        if (button != null)
        {
            button.interactable = unlocked;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClick);
        }
    }


    private void HandleClick()
    {
        onClick?.Invoke();
    }
}
