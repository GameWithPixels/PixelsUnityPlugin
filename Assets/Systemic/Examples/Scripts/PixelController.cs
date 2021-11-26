using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Systemic.Unity.Pixels;
using UnityEngine;

namespace Systemic.Unity.Examples
{
    [RequireComponent(typeof(DicePool))]
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
            foreach (var p in DicePool.Instance.AvailablePixels)
            {
                if (_pixelsRoot.OfType<Transform>().All(t => t.name != p.name))
                {
                    AddPixelPanel(p);
                }
            }
        }

        #endregion

        #region Public methods

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

        public void StartScan()
        {
            DicePool.Instance.ScanForPixels();
        }

        public void StopScan()
        {
            DicePool.Instance.StopScanForPixels();
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
