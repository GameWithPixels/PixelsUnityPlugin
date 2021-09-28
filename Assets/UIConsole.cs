using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIConsole : MonoBehaviour
{
    public Text LogText;
    public bool StartHidden;

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

        if (StartHidden)
        {
            gameObject.SetActive(false);
        }
    }
}
