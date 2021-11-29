using System.Collections;
using Systemic.Unity.Pixels;
using UnityEngine;
using UnityEngine.UI;

namespace Systemic.Unity.Examples
{
    /// <summary>
    /// Show information about a <see cref="Pixels.Pixel"/> and buttons to connect, disconnect and remove a Pixel.
    /// </summary>
    public class UIPixelPanel : MonoBehaviour
    {
        [SerializeField]
        Text _connectionState = null;

        [SerializeField]
        Text _name = null;

        [SerializeField]
        Text _battery = null;

        [SerializeField]
        Text _rssi = null;

        [SerializeField]
        Text _version = null;

        [SerializeField]
        Text _design = null;

        [SerializeField]
        Text _rollstate = null;

        [SerializeField]
        Text _face = null;

        [SerializeField]
        Button _connectButton = null;

        [SerializeField]
        Button _disconnectButton = null;

        /// <summary>
        /// Gets the attached <see cref="Pixels.Pixel"/>, see <see cref="AttachToPixel(Pixel)"/>.
        /// </summary>
        public Pixel Pixel { get; private set; }

        Coroutine _refreshInfoCoroutine;

        #region Unity messages

        // Start is called before the first frame update
        void Start()
        {
            _connectButton.interactable = _disconnectButton.interactable = false;
        }

        // Update is called once per frame
        void Update()
        {
            if (Pixel != null)
            {
                _connectionState.text = Pixel.connectionState.ToString();
                _name.text = Pixel.name;
                int battery = Mathf.RoundToInt(100 * Pixel.batteryLevel);
                string charging = Pixel.isCharging ? "charging" : "not charging";
                _battery.text = $"{battery}%, {charging}";
                _rssi.text = Pixel.rssi.ToString();
                _version.text = Pixel.firmwareVersionId;
                _design.text = Pixel.designAndColor.ToString();
                _rollstate.text = Pixel.rollState.ToString();
                _face.text = $"{Pixel.face + 1} of {Pixel.faceCount}";

                _connectButton.interactable = Pixel.isAvailable;
                _disconnectButton.interactable = Pixel.isReady;
            }
            else
            {
                _connectionState.text = _name.text = _battery.text = _rssi.text
                    = _version.text = _design.text = _rollstate.text = _face.text = "N/A";

                _connectButton.interactable = _disconnectButton.interactable = false;
            }
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Attach the instance to a <see cref="Pixels.Pixel"/> instance (replaces any previous attachment).
        /// </summary>
        /// <param name="pixel">The <see cref="Pixels.Pixel"/> instance to attach to.</param>
        public void AttachToPixel(Pixel pixel)
        {
            if (pixel == null) throw new System.ArgumentNullException(nameof(pixel));

            name = pixel.name;
            if (Pixel != pixel)
            {
                Pixel = pixel;
                Pixel.ConnectionStateChanged += (pixel, oldState, newState) =>
                {
                    if (newState == PixelConnectionState.Ready)
                    {
                        Debug.Assert(_refreshInfoCoroutine == null);
                        _refreshInfoCoroutine = StartCoroutine(RefreshRssiAndBattery());
                        IEnumerator RefreshRssiAndBattery()
                        {
                            while (true)
                            {
                                yield return pixel.UpdateRssiAsync();
                                yield return pixel.UpdateBatteryLevelAsync();
                                yield return new WaitForSecondsRealtime(5);
                            }
                        }
                    }
                    else if (_refreshInfoCoroutine != null)
                    {
                        StopCoroutine(_refreshInfoCoroutine);
                        _refreshInfoCoroutine = null;
                    }
                };
            }
        }

        /// <summary>
        /// Attempts to connect to the <see cref="Pixels.Pixel"/>.
        /// </summary>
        public void Connect()
        {
            DiceBag.Instance.ConnectPixel(Pixel, () => !isActiveAndEnabled);
        }

        /// <summary>
        /// Disconnects from the <see cref="Pixels.Pixel"/>.
        /// </summary>
        public void Disconnect()
        {
            DiceBag.Instance.DisconnectPixel(Pixel);
        }

        /// <summary>
        /// Remove the <see cref="Pixels.Pixel"/> from the list.
        /// </summary>
        public void Forget()
        {
            if (Pixel)
            {
                DiceBag.Instance.UnregisterPixel(Pixel);
            }
            Destroy(gameObject);
        }

        #endregion
    }
}
