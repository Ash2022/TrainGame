using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEngine;
using Voodoo.Tiny.Sauce.Internal;

namespace Voodoo.Tiny.Sauce.Common.Utils
{
      public class UnityThreadExecutor : MonoBehaviour
    {
        private const string TAG = "UnityThreadExecutor";
        private static UnityThreadExecutor _instance;

        private static Thread _mainThreadID;

        private static readonly List<Action> _eventsQueue = new List<Action>();

        private static volatile bool _eventsQueueEmpty = true;

        /// <summary>
        /// Initialize the class. Must be called from the Unity main thread an earliest as possible.
        /// </summary>
        public static void Initialize()
        {
            if (_instance != null) {
                return;
            }

            if (Application.isPlaying)
            {
                // Add an invisible game object to the scene
                var obj = new GameObject("UnityThreadExecutor") {hideFlags = HideFlags.HideAndDontSave};
                DontDestroyOnLoad(obj);
                _instance = obj.AddComponent<UnityThreadExecutor>();
            }
            _mainThreadID = Thread.CurrentThread;
        }

        public void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Call Execute function in try catch with log trace
        /// </summary>
        /// <param name="action">
        /// The action to execute safely
        ///</param>
        public static void SafeExecute(Action action)
        {
            try {
                Execute(action);
            } catch (Exception e) {
                VoodooLog.LogE(TAG, e.Message);
            }
        }

        /// <summary>
        /// Invoke the action directly if the current thread is the Unity main thread or as soon as we return to the Unity main thread
        /// </summary>
        /// <param name="action">
        /// The action to execute 
        ///</param>
        public static void Execute(Action action)
        {
            if (IsMainThread) action?.Invoke();
            else ExecuteInUpdate(action);
        }

        /// <summary>
        /// Invoke the action as soon as we return to the Unity main thread
        /// </summary>
        /// <param name="action">
        /// The action to execute 
        ///</param>
        public static void ExecuteInUpdate(Action action)
        {
            lock (_eventsQueue) {
                _eventsQueue.Add(action);
                _eventsQueueEmpty = false;
            }
        }

        /// <summary>
        /// True if the current thread is the Unity main thread.
        /// </summary>
        public static bool IsMainThread => _mainThreadID == Thread.CurrentThread;

        public void Update()
        {
            if (_eventsQueueEmpty) {
                return;
            }

            var stagedAdEventsQueue = new List<Action>();

            lock (_eventsQueue) {
                stagedAdEventsQueue.AddRange(_eventsQueue);
                _eventsQueue.Clear();
                _eventsQueueEmpty = true;
            }

            foreach (Action stagedEvent in stagedAdEventsQueue) {
                stagedEvent.Invoke();
            }
        }

        public void OnDisable()
        {
            _instance = null;
        }
    }

}