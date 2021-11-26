using System.Collections;
using System.Collections.Generic;
using Systemic.Unity.Pixels;
using UnityEngine;
using UnityEngine.UI;

namespace Systemic.Unity.Examples
{
    [RequireComponent(typeof(Toggle))]
    public class UIUpdateScanToggle : MonoBehaviour
    {
        Toggle _toggle;

        // Start is called before the first frame update
        void Start()
        {
            _toggle = GetComponent<Toggle>();
        }

        // Update is called once per frame
        void Update()
        {
            _toggle.isOn = DiceBag.Instance.IsScanning;
        }
    }
}
