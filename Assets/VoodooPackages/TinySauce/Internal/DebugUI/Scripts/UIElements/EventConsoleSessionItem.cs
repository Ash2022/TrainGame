using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Voodoo.Tiny.Sauce.Common.Extension;

public class EventConsoleSessionItem : MonoBehaviour
{
    [SerializeField] private Text title;
    [SerializeField] private Transform eventsContainer;
    [SerializeField] private Button copyButton;

    private int _sessionCount;
    private string _sessionId;


    public void Initialize(int sessionCount, string sessionId)
    {
        _sessionCount = sessionCount;
        _sessionId = sessionId; 
    }
        
    // Start is called before the first frame update
    void Start()
    {
        SetupUi();
    }
    
    private void SetupUi()
    {
        if(title != null) 
            title.text = "Session " + _sessionCount + ": " + _sessionId;
        if(copyButton != null) 
            copyButton.onClick.AddListener(() => _sessionId.CopyToClipboard());
    }
    

    public Transform GetContainer => eventsContainer;
}
