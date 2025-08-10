using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameOverView : MonoBehaviour
{
    [SerializeField] TMP_Text captionText;
    [SerializeField] TMP_Text buttonCaptionText;
    [SerializeField] Button actionButton;

    private Action _onPressed;

    void Awake()
    {
        if (actionButton != null)
            actionButton.onClick.AddListener(OnButtonClicked);
        gameObject.SetActive(false);
    }

    public void ShowWin(Action onNext)
    {
        Setup("Game Over\nYou Won", "Next", onNext);
    }

    public void ShowLose(Action onRetry)
    {
        Setup("Game Over\nYou Lost", "Retry", onRetry);
    }

    public void Hide()
    {
        _onPressed = null;
        gameObject.SetActive(false);
    }

    private void Setup(string caption, string btnText, Action callback)
    {
        if (captionText != null) captionText.text = caption;
        if (buttonCaptionText != null) buttonCaptionText.text = btnText;
        _onPressed = callback;
        gameObject.SetActive(true);
    }

    private void OnButtonClicked()
    {
        var cb = _onPressed;
        Hide();
        cb?.Invoke();
    }
}

