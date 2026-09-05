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

    [Header("Card State")]
    [SerializeField] private Sprite normalBackground;
    [SerializeField] private Sprite selectedBackground;

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
            SetSelected(false);
            button.interactable = unlocked;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClick);
        }
    }


    public void SetSelected(bool selected)
    {
        if (button == null || button.image == null)
            return;

        Sprite target = selected ? selectedBackground : normalBackground;
        if (target != null)
            button.image.sprite = target;
    }


    private void HandleClick()
    {
        onClick?.Invoke();
    }
}
