using DG.Tweening;

using TMPro;

using UnityEngine;


public class UserMessageView : MonoBehaviour
{
    [SerializeField] TMP_Text mainText;
    [SerializeField] CanvasGroup mainCanvasGroup;


    Sequence messageSeq;

    public void ShowAutoMessage(string message,Vector2 position, float waitTime=0)
    {
        transform.localPosition = position;

        mainCanvasGroup.alpha = 0.05f;

        if (messageSeq != null)
            messageSeq.Kill();

        mainText.text = "";

        messageSeq = DOTween.Sequence();

        messageSeq.Append(mainCanvasGroup.DOFade(1, 0.1f).OnStart(()=>
        {
            mainText.text = message;
        }));

        if(waitTime>0)
        {
            messageSeq.Join(DOVirtual.Float(0, 1, waitTime,(val) =>
            {

            })).OnComplete(() =>
            {
                HideMessage();
            });
        }

        messageSeq.Play();
        

    }

    public void HideMessage()
    {
        if (messageSeq != null)
            messageSeq.Kill();


        messageSeq = DOTween.Sequence();

        messageSeq.Append(mainCanvasGroup.DOFade(0, 0.1f));

        messageSeq.Play();

    }

    internal void UpdateMessage(string v)
    {
        mainText.text = v;


    }
}
