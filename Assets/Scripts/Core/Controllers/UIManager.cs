using DG.Tweening;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;



public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] TMP_Text resultText;
    [SerializeField] UserMessageView userMessageView;


    [SerializeField] List<Sprite> tutorialImages = new List<Sprite>();
    [SerializeField] TutorialImageView tutorialImageView;
    [SerializeField] RectTransform tutorialHand;
    [SerializeField] GameObject lockedIndicationPrefab;

    [SerializeField] Transform dynamicUIElementsHolder;

    Sequence handSequence;
 
    public Transform DynamicUIElementsHolder { get => dynamicUIElementsHolder; set => dynamicUIElementsHolder = value; }

    int levelTotalPassengers = 0;

    private void Awake()
    {
        Instance = this;
    }

    public void InitLevel(LevelData levelData,int levelIndex)
    {
        userMessageView.HideMessage();

        levelText.text = "LEVEL "+(levelIndex+1).ToString();

        //show the total passengers in the level

        levelTotalPassengers = Utils.GetTotalPassengers(levelData);

        resultText.text = "0/" + levelTotalPassengers;
    }

    public void UpdateScore(int totalCollected, int currentTotalArrived)
    {
        resultText.text = currentTotalArrived + "/" + levelTotalPassengers;
    }


    public void ShowTutorialImage(bool show, int imageIndex)
    {
        if (show)
        {
            levelText.text = "";

            if (imageIndex == 0)
                tutorialImageView.ShowTutorial(tutorialImages[0]);
            else
            {
                Sprite auxImage = null;

                if (imageIndex == 1)
                    auxImage=ModelManager.Instance.GetUnlockedColorSprite(0);

                if (imageIndex == 3)
                    auxImage = ModelManager.Instance.GetUnlockedColorSprite(1);

                tutorialImageView.ShowTutorial(tutorialImages[imageIndex],auxImage);
            }
        }
        else
        {
            //hide
            tutorialImageView.HideTutorial();
        }
    }

    public bool IsTutorialImageShowing()
    {
        return tutorialImageView.gameObject.activeInHierarchy;
    }

    public void ShowTutorialHand(Vector3 position,int index)
    {
        if(handSequence!=null)
            handSequence.Kill();

        tutorialHand.localScale = Vector3.one;

        tutorialHand.localPosition = GameManager.Instance.WorldToRect(position);

        tutorialHand.gameObject.SetActive(true);

        handSequence = DOTween.Sequence();

        handSequence.Append(tutorialHand.DOScale(0.8f, .8f).SetEase(Ease.InOutSine).SetLoops(100, LoopType.Yoyo));

        handSequence.Play();

        if(index == 1)
        {
            userMessageView.ShowAutoMessage("Click the truck to select it", new Vector2(0,-300), 0);
        }
        else if(index == 2)
        {
            userMessageView.UpdateMessage("Great,Click the station to select it");
        }
        else if (index == 3 || index == 5)
        {
            userMessageView.UpdateMessage("Click again to confirm");
        }
        else if(index == 4)
        {
            userMessageView.ShowAutoMessage("Click the depot to select it", new Vector2(0, -300), 0);
        }
    }

    internal void HideTutorialHand(bool hideAlsoText=false)
    {
        if (handSequence != null)
            handSequence.Kill();

        tutorialHand.gameObject.SetActive(false);

        if (hideAlsoText)
            userMessageView.HideMessage();

    }
    public GameObject GenerateLockedIndication(Vector3 position, int displayValue)
    {
        GameObject lockedIndicationObject = Instantiate(lockedIndicationPrefab, dynamicUIElementsHolder);

        Vector3 rectPosition = GameManager.Instance.WorldToRect(position);

        lockedIndicationObject.GetComponent<RectTransform>().localPosition = new Vector3(rectPosition.x, rectPosition.y-50, rectPosition.z);
        LockedIndicationView lockedIndicationView = lockedIndicationObject.GetComponent<LockedIndicationView>();
        lockedIndicationView.SetValue(displayValue);
        return lockedIndicationObject;
    }

    public void ClearDynamicHolder()
    {
        // clear out any previously spawned parts
        for (int i = dynamicUIElementsHolder.childCount - 1; i >= 0; i--)
            Destroy(dynamicUIElementsHolder.GetChild(i).gameObject);
    }

    internal void ShowUserMessage(string message, Vector2 position, float AutoRemoveTime, bool show)
    {
        if (show)
            userMessageView.ShowAutoMessage(message, position, AutoRemoveTime);
        else
            userMessageView.HideMessage();

    }

    


    /*
    public GameObject GenerateHiddenTilesIndication(Vector3 worldPos)
    {
        GameObject hiddenTilesIndication = Instantiate(hiddenTilesPrefab,dynamicUIElementsHolder);

        hiddenTilesIndication.GetComponent<RectTransform>().localPosition = TileStacksGameManager.Instance.WorldToRect(worldPos) - new Vector2(0,60);

        return hiddenTilesIndication;
    }*/
    /*
    internal void GenerateCounterEffect(int count, float delay, int colorIndex, TileStacksColorButtonView clickedButton)
    {
        GameObject counterEffectGO = Instantiate(counterEffectPrefab, dynamicUIElementsHolder);

        counterEffectGO.GetComponent<RectTransform>().localPosition = TileStacksGameManager.Instance.WorldToRect(clickedButton.gameObject.transform.position);

        RectTransform counterRect = counterEffectGO.GetComponent<RectTransform>();
        TMP_Text counterText = counterEffectGO.GetComponent<TMP_Text>();
        counterText.text = "0";
        
        counterText.color = TileStacksUtils.GetLessSaturatedColor(TileStacksModelManager.Instance.GetTileColor(colorIndex), 0.35f);

        float startY = counterRect.localPosition.y;

        counterRect.DOAnchorPosY(startY + (count * 10.5f),delay).OnComplete(()=>
        {           

            if(count> MIN_COMBO_FOR_WORD)
            {
                if(spriteCombo)
                {
                    GameObject wordEffectGO = Instantiate(wordEffectPrefab, dynamicUIElementsHolder);
                    Image effectImage = wordEffectGO.GetComponent<Image>();
                    effectImage.sprite = completionWords[GetWordIndex(count)];
                    effectImage.SetNativeSize();
                    CanvasGroup effectCanvas = wordEffectGO.GetComponent<CanvasGroup>();
                    RectTransform effectRect = wordEffectGO.GetComponent<RectTransform>();
                    effectCanvas.alpha = 0;

                    //put the effect on the number and show the word

                    effectRect.localPosition = counterRect.localPosition;
                    effectRect.localScale = Vector3.one * 0.25f;

                    float startY = effectRect.localPosition.y;

                    effectRect.DOLocalMove(new Vector2(0, startY + 200), 1);
                    effectCanvas.DOFade(1, 0.25f).OnComplete(() =>
                    {
                        effectCanvas.DOFade(0, 0.15f).SetDelay(0.6f).OnComplete(() =>
                        {
                            Destroy(wordEffectGO);
                        });
                    });
                    effectRect.DOScale(1f, 0.95f);
                }
                else
                {
                    GameObject wordEffectGO = Instantiate(wordEffectPrefab, dynamicUIElementsHolder);
                    //Image effectImage = wordEffectGO.GetComponent<Image>();
                    //effectImage.sprite = completionWords[GetWordIndex(count)];
                    //effectImage.SetNativeSize();
                    TMP_Text effectText = wordEffectGO.GetComponent<TMP_Text>();

                    effectText.color = TileStacksUtils.GetLessSaturatedColor(TileStacksModelManager.Instance.GetTileColor(colorIndex),0.35f);
                    effectText.text = GetWordString(count);

                    CanvasGroup effectCanvas = wordEffectGO.GetComponent<CanvasGroup>();
                    RectTransform effectRect = wordEffectGO.GetComponent<RectTransform>();
                    effectCanvas.alpha = 0;

                    //put the effect on the number and show the word

                    effectRect.localPosition = counterRect.localPosition;
                    effectRect.localScale = Vector3.one * 0.35f;

                    float startY = effectRect.localPosition.y;

                    effectRect.DOLocalMoveY(startY + 380, 1.15f).SetEase(Ease.OutBack);
                    effectRect.DOLocalMoveX(0, 1.15f).SetEase(Ease.OutExpo);



                    effectCanvas.DOFade(1, 0.25f).OnComplete(() =>
                    {
                        effectCanvas.DOFade(0, 0.15f).SetDelay(0.75f).OnComplete(() =>
                        {
                            Destroy(wordEffectGO);
                        });
                    });
                    effectRect.DOScale(1.15f, 0.95f).SetEase(Ease.OutExpo);
                }


            }

            //destory the Game object and generate an effect for big/huge....
            Destroy(counterEffectGO);

        });
        DOVirtual.Int(0, count, delay-0.05f, (countValue) =>
        {
            counterText.text = countValue.ToString();
            counterText.fontSize = 50 + countValue;
        });

    }
    */

}
