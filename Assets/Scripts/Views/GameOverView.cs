using DG.Tweening;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameOverView : MonoBehaviour
{
    [SerializeField] CanvasGroup mainCanvasGroup;

    [SerializeField] TMP_Text titleText;
    
    [SerializeField] Button endGameButton;
    [SerializeField] Image buttonImage;

    [SerializeField] Sprite continueButton;
    [SerializeField] Sprite tryAgainButton;
    [SerializeField] GameObject completeStar;
    

    [SerializeField] EndScreenUnlockView endScreenUnlockView;
    int currLevelIndex;
    bool noMoreUnlocks;
    Action endGameComplete;

    public void InitEndScreen(bool levelWon, int levelIndex, Action endGameResumeClicked)
    {
        currLevelIndex = levelIndex;
        endScreenUnlockView.BackParticles.SetActive(false);
        endScreenUnlockView.CanvasGroup.alpha = 0;

        mainCanvasGroup.alpha = 0;

        completeStar.SetActive(levelWon);

        gameObject.SetActive(true);

        endGameComplete = endGameResumeClicked;

        endGameButton.interactable = false;
        buttonImage.sprite = levelWon ? continueButton : tryAgainButton;
        buttonImage.gameObject.SetActive(false );

        bool stillHaveUnlocks = UnlocksFinished();

        if (levelWon)
        {
            titleText.text = "TABLE CLEARED\n\n<size=120>LEVEL " + (levelIndex + 1) + "\nCOMPLETE";

            if (stillHaveUnlocks)
            {
                titleText.transform.localPosition = new Vector3(0, 350, 0);
                completeStar.transform.localPosition = new Vector3(0,-100,0);
            }
            else
            {
                titleText.transform.localPosition = new Vector3(0, 600, 0);
                completeStar.transform.localPosition = new Vector3(0, 250, 0);
                
            }
        }
        else
        {
            titleText.text = "LEVEL " + (levelIndex + 1) + "\nFAILED";

            titleText.transform.localPosition = Vector3.zero;
            endScreenUnlockView.HideEndScreenUnlocks();

        }

        mainCanvasGroup.DOFade(1, 1).SetDelay(0.35f).OnComplete(() =>
        {

            if (levelWon)
            {
                SoundsManager.Instance.PlayLevelCompelte();

                InitEndScreenUnlockView();

                if (!noMoreUnlocks)
                {
                    ProgressUnlockView(()=>
                    {
                        ShowAndEnableContinue();
                    });
                }
                else
                    ShowAndEnableContinue();
            }
            else
                {
                    SoundsManager.Instance.PlayLevelFailed();
                    ShowAndEnableContinue();
                }
        });
    }

    private void ShowAndEnableContinue()
    {
        endGameButton.interactable = true;
        buttonImage.gameObject.SetActive(true);
    }

    public void EndGameButtonClicked()
    {
        gameObject.SetActive(false);
        endGameComplete?.Invoke();
    }


    private bool UnlocksFinished()
    {
        List<int> unlocksIndexList = ModelManager.Instance.UnlocksIndexList;

        //now i need to see where i am in this list

        int startIndex = 0;
        int endIndex = 0;
        int presentIndex = 0;

        if (unlocksIndexList != null)
        {
            for (int i = 0; i < unlocksIndexList.Count; i++)
            {
                if (currLevelIndex < unlocksIndexList[i] && endIndex == 0)
                {
                    endIndex = unlocksIndexList[i];
                    presentIndex = i;

                    if (i > 0)
                        startIndex = unlocksIndexList[i - 1];

                }

            }
        }

        return endIndex == 0;   
    }


    private void InitEndScreenUnlockView()
    {
        //init to show the current state

        //if win - need to increase the progress bar - need to update the text value

        //need to check the level i am on - then see all the unlocks - see where i am - set the total and current steps

        List<int> unlocksIndexList = ModelManager.Instance.UnlocksIndexList;

        //now i need to see where i am in this list

        int startIndex = 0;
        int endIndex = 0;
        int presentIndex = 0;

        if(unlocksIndexList != null)
        {
            for (int i = 0; i < unlocksIndexList.Count; i++)
            {
                if (currLevelIndex < unlocksIndexList[i] && endIndex == 0)
                {
                    endIndex = unlocksIndexList[i];
                    presentIndex = i;

                    if (i > 0)
                        startIndex = unlocksIndexList[i - 1];

                }

            }
        }


        //if end index == 0 -- no more unlocks to show

        if (endIndex == 0)
        {
            noMoreUnlocks = true;
            endScreenUnlockView.HideEndScreenUnlocks();
        }
        else
        {

            int total = endIndex - startIndex;
            int curr = currLevelIndex - startIndex;

            Debug.Log("presentIndex " + presentIndex);

            Color unlockColor = Color.white;

            //endScreenUnlockView.InitDisplay(unlockColor, total, curr);
            

            endScreenUnlockView.InitDisplay(ModelManager.Instance.GetUnlockImage(presentIndex, true), ModelManager.Instance.GetUnlockImage(presentIndex, false),total, curr);
        }


    }
    private void ProgressUnlockView(Action done)
    {
        //need to see if need to update the model for powerUps 
        //SoundsController.Instance.PlayProgressBar();
        endScreenUnlockView.UpdateProgress(done);
    }
}
