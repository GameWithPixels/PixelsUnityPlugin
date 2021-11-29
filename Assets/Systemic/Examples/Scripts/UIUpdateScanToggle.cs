using Systemic.Unity.Pixels;
using UnityEngine;
using UnityEngine.UI;

namespace Systemic.Unity.Examples
{
    /// <summary>
    /// Specialized class that updates the state of a <see cref="Toggle"/> instance
    /// based on whether the <see cref="DiceBag"/> is scanning for Pixels.
    /// </summary>
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
