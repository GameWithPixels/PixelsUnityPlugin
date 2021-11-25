using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Systemic.Unity.Examples
{
    /// <summary>
    /// Gets the log messages and copy them to a UI.Text object.
    /// </summary>
    public class UILogToText : MonoBehaviour
    {
        /// <summary>
        /// All log messages are copied to this UI text object.
        /// </summary>
        public Text LogText;

        void OnEnable()
        {
            Application.logMessageReceived += LogCallback;
        }

        void OnDisable()
        {
            Application.logMessageReceived -= LogCallback;
        }

        void LogCallback(string logString, string stackTrace, LogType type)
        {
            LogText.text += logString + "\n";
        }

        void Start()
        {
            LogText.text = "";
        }
    }
}
