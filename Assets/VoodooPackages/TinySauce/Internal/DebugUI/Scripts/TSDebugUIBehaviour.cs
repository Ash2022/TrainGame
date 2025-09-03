using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Voodoo.Tiny.Sauce.Internal.Debugger
{
    public class TSDebugUIBehaviour : MonoBehaviour
    {
        private const string TAG = "TSDebugUIBehaviour";
        
        [Header("== Buttons ==")]
        [Space(4)]
        [SerializeField] private Button infoTabBtn;
        [SerializeField] private Button eventsTabBtn;
        [SerializeField] private Button abtestTabBtn;
        [SerializeField] private Button eventLoggerBtn;
        public TSDebugUIActiveScreen test;

        [Header("== Screens ==")]
        [SerializeField] private TSDebugUIScreen infoScreen;
        [SerializeField] private TSDebugUIScreen eventsScreen;
        [SerializeField] private TSDebugUIScreen abtestScreen;
        [SerializeField] private TSDebugUIScreen eventLoggerScreen;
        [SerializeField] private TSDebugUIScreen eventInformationScreen;
        [SerializeField] private TSDebugUIScreen eventFilterScreen;

        [Header("== App Info Fields ==")]
        [SerializeField] private Text unityVerion;
        [SerializeField] private Text tsVersion;
        [SerializeField] private Text appNameTop;


        private static TSDebugUIBehaviour _instance;
        public static TSDebugUIBehaviour Instance { get => _instance; }

        
        private EventSystem _eventSystemPrefab;
        private EventSystem _eventSystem;
        
        private TinySauceSettings _tsSettings;

        private TSDebugUIActiveScreen activeScreen = TSDebugUIActiveScreen.Info;
        private Dictionary<TSDebugUIActiveScreen, Button> tabDictionary = new Dictionary<TSDebugUIActiveScreen, Button>();
        private Dictionary<TSDebugUIActiveScreen, TSDebugUIScreen> screenDictionary = new Dictionary<TSDebugUIActiveScreen, TSDebugUIScreen>();


        private void Awake()
        {
            if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            //if (FindObjectOfType<EventSystem>() == null) Instantiate(new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)));
            InitEventSystem();

            _tsSettings = TinySauceSettings.Load();
            UpdateInfo();
            bool hasAbTestPackage = TinySauceBehaviour.ABTestManager != null;
            abtestTabBtn.gameObject.SetActive(hasAbTestPackage);

            infoScreen.TSSettings = _tsSettings;
        }

        private void Start()
        {
            InitDictionaries();
            SetInfoScreenActive();
        }

        private void OnDestroy()
        {
            _instance = null;
        }


        private  void InitEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            
            if (_eventSystemPrefab == null)
                _eventSystemPrefab = Resources.LoadAll<EventSystem>("Prefabs")[0];
                    
            if (_eventSystemPrefab == null)
                Debug.LogError("There is no TSEventSystem prefab in the 'Assets/VoodooPackages/TinySauce/Resources/Prefabs' folder");

            _eventSystem = Instantiate(_eventSystemPrefab);
        }
        
        public void CloseDebugUI()
        {
            if (_eventSystem != null)
                Destroy(_eventSystem.gameObject);
            
            Destroy(gameObject);
        }

        private void UpdateInfo()
        {
            unityVerion.text = Application.unityVersion;
            tsVersion.text = "TS v. " + TinySauce.Version;
            appNameTop.text = Application.productName;
        }

        #region [TABS]
        private void InitDictionaries()
        {
            tabDictionary[TSDebugUIActiveScreen.Info] = infoTabBtn;
            tabDictionary[TSDebugUIActiveScreen.Events] = eventsTabBtn;
            tabDictionary[TSDebugUIActiveScreen.ABTest] = abtestTabBtn;
            tabDictionary[TSDebugUIActiveScreen.EventConsole] = eventLoggerBtn;

            screenDictionary[TSDebugUIActiveScreen.Info] = infoScreen;
            screenDictionary[TSDebugUIActiveScreen.Events] = eventsScreen;
            screenDictionary[TSDebugUIActiveScreen.ABTest] = abtestScreen;
            screenDictionary[TSDebugUIActiveScreen.EventConsole] = eventLoggerScreen;
            screenDictionary[TSDebugUIActiveScreen.EventInformation] = eventInformationScreen;
            screenDictionary[TSDebugUIActiveScreen.EventFilters] = eventFilterScreen;

            infoScreen.gameObject.SetActive(false);
            eventsScreen.gameObject.SetActive(false);
            abtestScreen.gameObject.SetActive(false);
            eventLoggerScreen.gameObject.SetActive(false);
            eventInformationScreen.gameObject.SetActive(false);

            if (TinySauceBehaviour.ABTestManager == null || TinySauceBehaviour.ABTestManager.GetAbTestValues().Length == 0)
            {
                abtestTabBtn.interactable = false;
                abtestTabBtn.image.color = new Color(1, 0.75f, 0.75f);
            }
        }

        private void ToggleTab(bool isActive)
        {
            if(activeScreen != TSDebugUIActiveScreen.EventInformation && activeScreen != TSDebugUIActiveScreen.EventFilters)
                tabDictionary[activeScreen].interactable = !isActive;
            screenDictionary[activeScreen].OnScreenHide();
            screenDictionary[activeScreen].gameObject.SetActive(isActive);
        }
        public void SetInfoScreenActive()
        {
            SetActiveScreen(TSDebugUIActiveScreen.Info);
        }        
        
        public void SetTestEventsScreenActive()
        {
            SetActiveScreen(TSDebugUIActiveScreen.Events);
        }
        public void SetABTestScreenActive()
        {
            SetActiveScreen(TSDebugUIActiveScreen.ABTest);
        }
        public void SetEventConsoleScreenActive()
        {
            SetActiveScreen(TSDebugUIActiveScreen.EventConsole);
        }

        public void SetEventConsoleInformation()
        {
            SetActiveScreen(TSDebugUIActiveScreen.EventInformation);
        }
        
        public void SetEventConsoleFilter()
        {
            SetActiveScreen(TSDebugUIActiveScreen.EventFilters);
        }


        private void SetActiveScreen(TSDebugUIActiveScreen screenName)
        {
            ToggleTab(false);
            activeScreen = screenName;
            ToggleTab(true);
        }
        #endregion []
    }
    
    [Serializable]
    public enum TSDebugUIActiveScreen
    {
        Info,
        Events,
        ABTest,
        EventConsole, 
        EventInformation,
        EventFilters
    }
}