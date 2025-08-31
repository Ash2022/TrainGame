using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TutorialImageView : MonoBehaviour
{
    [SerializeField] Image tutImage;
    [SerializeField] Image tutAuxImage;

    public void ShowTutorial(Sprite sprite,Sprite auxSprite=null)
    {
        tutImage.sprite = sprite;
        tutImage.SetNativeSize();

        if(auxSprite != null )//aux image is made for colors unlock ad its size is 5
        {
            tutAuxImage.sprite = auxSprite;
            tutAuxImage.gameObject.SetActive(true);
        }
        else
            tutAuxImage.gameObject.SetActive(false);

        gameObject.SetActive(true);
    }

    public void HideTutorial()
    {
        gameObject.SetActive(false);
    }
}
