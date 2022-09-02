using System.Linq;
using Systemic.Unity.Pixels;
using UnityEngine;

namespace Systemic.Unity.Examples
{
    /// <summary>
    /// Demonstrates how to scan for, connect to and retrieve information from Pixel dice
    /// using the dedicated <see cref="Pixel"/> and <see cref="DiceBag"/> classes.
    /// </summary>
    public class PixelController : MonoBehaviour
    {
        [SerializeField]
        RectTransform _pixelsRoot = null;

        UIPixelPanel _pixelPanelTemplate;

        #region Unity messages

        // Called when the instance becomes enabled and active
        void OnEnable()
        {
            //TODO handle error + user message
            BluetoothLE.Central.Initialize();
        }

        // Called when the instance becomes disabled or inactive
        void OnDisable()
        {
            BluetoothLE.Central.Shutdown();
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
            foreach (var p in DiceBag.AvailablePixels)
            {
                if (_pixelsRoot.OfType<Transform>().All(t => t.name != p.name))
                {
                    AddPixelPanel(p);
                }
            }

            var toRemove = _pixelsRoot.GetComponentsInChildren<UIPixelPanel>()
                .Where(p => !DiceBag.AllPixels.Contains(p.Pixel))
                .Select(p => p.gameObject)
                .ToArray();
            foreach (var gameObject in toRemove)
            {
                Destroy(gameObject);
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
            DiceBag.ScanForPixels();
        }

        /// <summary>
        /// Stop scanning for <see cref="Pixel"/>s.
        /// </summary>
        public void StopScan()
        {
            DiceBag.StopScanning();
        }

        /// <summary>
        /// Remove all scanned Pixels that are not connected.
        /// </summary>
        public void ClearScanList()
        {
            DiceBag.ClearAvailablePixels();
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
