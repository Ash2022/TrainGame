using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Voodoo.Tiny.Sauce.Internal.Analytics;

namespace Voodoo.Tiny.Sauce.Internal.Debugger
{
    public class TSDebugUIFiltersScreen : TSDebugUIScreen
    {
        private const string DEFAULT_WRAPPER_NAME_FILTER = "VoodooAnalytics";
        
        [Header("EventFilters"), SerializeField]
        private Transform filtersContainer;

        private readonly Queue<DebugToggleButton> _eventFilterItemsPool = new Queue<DebugToggleButton>();
        private readonly List<string> _excludedEventFilters = new List<string>();
        internal TSDebugUIScreenEventConsole EventConsoleListScreen { private get; set; }

        private DebugToggleButton _masterItem;
        
        
        
        private List<DebugToggleButton> _eventFilters = new List<DebugToggleButton>();
        [SerializeField] private DebugToggleButton toggleButtonPrefab;
        
        private void OnEnable()
        {
            ClearFiltersBody();
            FillFiltersBody();
            AddMasterFilter();
        }
        
        public override void OnScreenHide()
        {
            EventConsoleListScreen.RefreshEventLogToScreen();
        }
        
        private IEnumerable<KeyValuePair<string, bool>> GetFiltersWithStates()
        {
            var filtersStates = new SortedDictionary<string, bool>();
            List<DebugAnalyticsLog> analyticsLogs = AnalyticsEventLogger.GetInstance().GetLocalAnalyticsLog(DEFAULT_WRAPPER_NAME_FILTER);
            foreach (DebugAnalyticsLog log in analyticsLogs) {
                filtersStates[log.EventName] = true;
            }

            if (_excludedEventFilters != null) {
                foreach (string filter in _excludedEventFilters.Where(filter => filtersStates.ContainsKey(filter))) {
                    filtersStates[filter] = false;
                }
            }
            return filtersStates;
        }
        
        private void ClearFiltersBody()
        {
            foreach (Transform filterItem in filtersContainer) {
                filterItem.gameObject.SetActive(false);
                _eventFilterItemsPool.Enqueue(filterItem.GetComponent<DebugToggleButton>());
            }
        }

        private DebugToggleButton AddToggleButton(string label, bool value, Action<bool> callback)
        {
            DebugToggleButton toggleButton = Instantiate(toggleButtonPrefab, filtersContainer);
            toggleButton.Initialize(label, value, callback);
            return toggleButton;
        }
        
        private void FillFiltersBody()
        {
            IEnumerable<KeyValuePair<string, bool>> filtersStates = GetFiltersWithStates();
            foreach (KeyValuePair<string, bool> filterState in filtersStates) {
                DebugToggleButton filterItem;
                if (_eventFilterItemsPool.Count == 0) {
                    filterItem = AddToggleButton("", false, null);
                    _eventFilters.Add(filterItem);
                    filterItem.transform.SetParent(filtersContainer);
                } else {
                    filterItem = _eventFilterItemsPool.Dequeue();
                    filterItem.gameObject.SetActive(true);
                    filterItem.SetCallback(null);
                }

                filterItem.Initialize(filterState.Key, filterState.Value, newState => UpdateSavedFilterList(filterState.Key, newState));
            }
        }
        
                
        private void AddMasterFilter()
        {
            const string masterToggleLabel = "Toggle All";
            bool defaultValue = _excludedEventFilters.Count == 0;
            if (_eventFilterItemsPool.Count == 0)
            {
                _masterItem = AddToggleButton(masterToggleLabel, defaultValue, ToggleAll);
                _masterItem.transform.SetParent(filtersContainer);
                _masterItem.transform.SetAsFirstSibling();
            }
            else
            {
                _masterItem = _eventFilterItemsPool.Dequeue();
                _masterItem.gameObject.SetActive(true);
                _masterItem.Initialize(masterToggleLabel, defaultValue, ToggleAll);
                _masterItem.transform.SetAsFirstSibling();
            }
        }
        
        private void UpdateMasterFilter()
        {
            _masterItem.SetCallback(null);
            _masterItem.SetValue(_excludedEventFilters.Count == 0);
            _masterItem.SetCallback(ToggleAll);
        }
        
        private void ToggleAll(bool newState)
        {
            foreach (DebugToggleButton eventFilter in _eventFilters)
            {
                eventFilter.SetValue(newState);
            }
        }
        
        private void UpdateSavedFilterList(string filter, bool isChecked)
        {
            if (isChecked && _excludedEventFilters.Contains(filter)) {
                _excludedEventFilters.Remove(filter);
            } else if (!isChecked && !_excludedEventFilters.Contains(filter)) {
                _excludedEventFilters.Add(filter);
            }
            
            UpdateMasterFilter();
        }
        
        public bool IsExcluded(DebugAnalyticsLog log)
        {
            return _excludedEventFilters != null && _excludedEventFilters.Any(filter => filter == log.EventName);
        }
        
        public IEnumerable<DebugAnalyticsLog> FilterEvents(IEnumerable<DebugAnalyticsLog> analyticsLogs)
        {
            if (_excludedEventFilters != null && _excludedEventFilters.Any()) {
                analyticsLogs = analyticsLogs.Where(analyticLog => _excludedEventFilters.All(filter => filter != analyticLog.EventName));
            }

            return analyticsLogs;
        }

    }
}