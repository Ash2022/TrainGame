using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameOverView : MonoBehaviour
{
    [SerializeField] TMP_Text captionText;
    [SerializeField] TMP_Text buttonCaptionText;
    [SerializeField] Button actionButton;
    [SerializeField] CanvasGroup gameOverCanvas;

    private Action _onPressed;

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
        gameOverCanvas.alpha = 0;
    }

    private void Setup(string caption, string btnText, Action callback)
    {
        if (captionText != null) captionText.text = caption;
        if (buttonCaptionText != null) buttonCaptionText.text = btnText;
        _onPressed = callback;
        gameOverCanvas.alpha = 1;
    }

    public void OnButtonClicked()
    {
        var cb = _onPressed;
        Hide();
        cb?.Invoke();
    }
}

