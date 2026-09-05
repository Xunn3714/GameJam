using TMPro;
using UnityEngine;

public class StatRowView : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text nameText;
    public TMP_Text valueText;

    public void Setup(string statName, string statValue)
    {
        if (nameText != null)
            nameText.text = statName;

        if (valueText != null)
            valueText.text = statValue;
    }
}
