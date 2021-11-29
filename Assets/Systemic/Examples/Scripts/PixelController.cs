using System.Linq;
using Systemic.Unity.Pixels;
using UnityEngine;

namespace Systemic.Unity.Examples
{
    /// <summary>
    /// Demonstrates how to scan for, connect to and retrieve information from Pixel dice
    /// using the dedicated <see cref="Pixel"/> class.
    /// </summary>
    [RequireComponent(typeof(DiceBag))]
    public class PixelController : MonoBehaviour
    {
        [SerializeField]
        RectTransform _pixelsRoot = null;

        UIPixelPanel _pixelPanelTemplate;

        #region Unity messages

        private void OnEnable()
        {
        }

        // Start is called before the first frame update
        void Start()
        {
            _pixelPanelTemplate = _pixelsRoot.GetChild(0).GetComponent<UIPixelPanel>();
            _pixelPanelTemplate.gameObject.SetActive(false);
        }

        // Update is called once per frame
        void Update()
        {
            foreach (var p in DiceBag.Instance.AvailablePixels)
            {
                if (_pixelsRoot.OfType<Transform>().All(t => t.name != p.name))
                {
                    AddPixelPanel(p);
                }
            }
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Toggle <see cref="Pixel"/> scanning on or off.
        /// </summary>
        /// <param name="startScan">Whether to start a scan/</param>
        public void ToggleScanning(bool startScan)
        {
            if (startScan)
            {
                StartScan();
            }
            else
            {
                StopScan();
            }
        }

        /// <summary>
        /// Start scanning for <see cref="Pixel"/>s.
        /// </summary>
        public void StartScan()
        {
            DiceBag.Instance.ScanForPixels();
        }

        /// <summary>
        /// Stop scanning for <see cref="Pixel"/>s.
        /// </summary>
        public void StopScan()
        {
            DiceBag.Instance.StopScanForPixels();
        }

        #endregion

        #region Private methods

        void AddPixelPanel(Pixel pixel)
        {
            var pixelPanel = Instantiate(_pixelPanelTemplate, _pixelsRoot);
            pixelPanel.gameObject.SetActive(true);
            pixelPanel.AttachToPixel(pixel);
        }

        #endregion
    }
}
