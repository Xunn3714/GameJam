using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class JoinToastView : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField, Min(0f)] private float displayDuration = 1.5f;

    private Coroutine hideCoroutine;

    private void Awake()
    {
        MvpTmpUiFont.Apply(messageText);
        HideImmediate();
    }

    public void Show(string sheepName)
    {
        if (messageText == null)
            return;

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
        }

        messageText.text = $"“{sheepName}”加入了族群！";
        messageText.gameObject.SetActive(true);

        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);

        HideImmediate();
        hideCoroutine = null;
    }

    private void HideImmediate()
    {
        if (messageText != null)
        {
            messageText.gameObject.SetActive(false);
        }
    }
}
