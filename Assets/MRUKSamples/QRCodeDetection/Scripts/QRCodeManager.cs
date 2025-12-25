// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.MRUtilityKit;
using Meta.XR.Samples;
using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // Required for TextMeshProUGUI

namespace Meta.XR.MRUtilityKitSamples.QRCodeDetection
{
    [MetaCodeSample("MRUKSample-QRCodeDetection")]
    public class QRCodeManager : MonoBehaviour
    {
        // ------------- Static Interface -------------
        public const string ScenePermission = OVRPermissionsRequester.ScenePermission;
        public static bool IsSupported => MRUK.Instance.QRCodeTrackingSupported;

        public static bool HasPermissions
#if UNITY_EDITOR
            => true;
#else
            => UnityEngine.Android.Permission.HasUserAuthorizedPermission(ScenePermission);
#endif

        public static int ActiveTrackedCount => s_instance ? s_instance._activeCount : 0;

        public static bool TrackingEnabled
        {
            get => s_instance && s_instance._mrukInstance && s_instance._mrukInstance.SceneSettings.TrackerConfiguration.QRCodeTrackingEnabled;
            set
            {
                if (!s_instance || !s_instance._mrukInstance) return;
                var config = s_instance._mrukInstance.SceneSettings.TrackerConfiguration;
                config.QRCodeTrackingEnabled = value;
                s_instance._mrukInstance.SceneSettings.TrackerConfiguration = config;
            }
        }

        public static void RequestRequiredPermissions(Action<bool> onRequestComplete)
        {
            if (!s_instance)
            {
                Debug.LogError($"{nameof(RequestRequiredPermissions)} failed; no QRCodeManager instance.");
                return;
            }
#if UNITY_EDITOR
            onRequestComplete?.Invoke(HasPermissions);
#else
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            if (onRequestComplete is not null)
            {
                callbacks.PermissionGranted += _ => onRequestComplete(HasPermissions);
                callbacks.PermissionDenied += _ => onRequestComplete(HasPermissions);
                callbacks.PermissionDeniedAndDontAskAgain += _ => onRequestComplete(HasPermissions);
            }
            UnityEngine.Android.Permission.RequestUserPermission(ScenePermission, callbacks);
#endif
        }

        // ------------- Serialized Fields -------------

        [Tooltip("Your World Space UI Prefab (Brackets + Text)")]
        [SerializeField] private GameObject _qrCodePrefab;

        [Tooltip("The UI Panel from the original sample to log debug messages")]
        [SerializeField] private QRCodeSampleUI _uiInstance;

        [SerializeField] private MRUK _mrukInstance;

        // ------------- Private Fields -------------

        private int _activeCount;
        private static QRCodeManager s_instance;

        // Helper class to store references for each active QR code
        private class ActiveQRData
        {
            public RectTransform VisualRect;
            public TextMeshProUGUI TextComponent;
            public MRUKTrackable Trackable;
        }

        private Dictionary<MRUKTrackable, ActiveQRData> _activeQRDict = new Dictionary<MRUKTrackable, ActiveQRData>();

        // ------------- MonoBehaviour Messages -------------

        void OnValidate()
        {
            if (!_uiInstance && FindAnyObjectByType<QRCodeSampleUI>() is { } ui && ui.gameObject.scene == gameObject.scene)
            {
                _uiInstance = ui;
            }
            if (!_mrukInstance && FindAnyObjectByType<MRUK>() is { } mruk && mruk.gameObject.scene == gameObject.scene)
            {
                _mrukInstance = mruk;
            }
        }

        void OnEnable()
        {
            s_instance = this;

            if (!_mrukInstance)
            {
                Log($"{nameof(QRCodeManager)} requires an MRUK object in the scene!", LogType.Error);
                return;
            }

            _mrukInstance.SceneSettings.TrackableAdded.AddListener(OnTrackableAdded);
            _mrukInstance.SceneSettings.TrackableRemoved.AddListener(OnTrackableRemoved);
        }

        void OnDestroy()
        {
            s_instance = null;
            if (_mrukInstance)
            {
                _mrukInstance.SceneSettings.TrackableAdded.RemoveListener(OnTrackableAdded);
                _mrukInstance.SceneSettings.TrackableRemoved.RemoveListener(OnTrackableRemoved);
            }
        }

        void Update()
        {
            // Iterate through all active QR codes
            foreach (var kvp in _activeQRDict)
            {
                ActiveQRData data = kvp.Value;
                MRUKTrackable trackable = data.Trackable;

                if (trackable != null && data.VisualRect != null && trackable.PlaneRect.HasValue)
                {
                    // 1. Get Physical Size
                    float width = trackable.PlaneRect.Value.width;
                    float height = trackable.PlaneRect.Value.height;

                    // 2. Scale the Bracket UI to fit perfectly
                    data.VisualRect.localScale = new Vector3(width, height, 1f);

                    // 3. Optional: Billboard Text (Make text always face camera)
                    if (data.TextComponent != null && Camera.main != null)
                    {
                        Vector3 dirToCam = data.TextComponent.transform.position - Camera.main.transform.position;
                        if (dirToCam != Vector3.zero)
                        {
                            data.TextComponent.transform.rotation = Quaternion.LookRotation(dirToCam);
                        }
                    }
                }
            }
        }

        // ------------- UnityEvent Listeners -------------

        public void OnTrackableAdded(MRUKTrackable trackable)
        {
            if (trackable.TrackableType != OVRAnchor.TrackableType.QRCode) return;

            // 1. Log Detection
            Log($"{nameof(OnTrackableAdded)}: QRCode detected!");

            // 2. Instantiate Prefab
            GameObject instance = Instantiate(_qrCodePrefab, trackable.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            // 3. Gather References
            ActiveQRData newData = new ActiveQRData();
            newData.Trackable = trackable;
            
            // Find Components
            newData.VisualRect = instance.GetComponent<RectTransform>();
            if (newData.VisualRect == null) newData.VisualRect = instance.GetComponentInChildren<RectTransform>();
            newData.TextComponent = instance.GetComponentInChildren<TextMeshProUGUI>();

            // 4. Handle Payload (Get String -> Set UI Text -> Log to Sample UI)
            string payload = trackable.MarkerPayloadString;
            
            // Handle binary data if string is empty
            if (string.IsNullOrEmpty(payload) && trackable.MarkerPayloadBytes != null)
            {
                 payload = $"Binary Data (Length: {trackable.MarkerPayloadBytes.Length})";
            }

            // A) Set text on the prefab itself (like the old MarkerController)
            if (newData.TextComponent != null)
            {
                newData.TextComponent.text = payload;
            }

            // B) Log payload to the Blue Sample UI Window
            Log($"Payload: {payload}");

            _activeQRDict.Add(trackable, newData);
            _activeCount++;
        }

        public void OnTrackableRemoved(MRUKTrackable trackable)
        {
            if (trackable.TrackableType != OVRAnchor.TrackableType.QRCode) return;

            Log($"QRCode removed");

            if (_activeQRDict.ContainsKey(trackable))
            {
                _activeQRDict.Remove(trackable);
            }

            _activeCount--;
            Destroy(trackable.gameObject);
        }

        // ------------- Private Logging Impl (Restored) -------------

        static void Log(object msg, LogType type = LogType.Log)
        {
            if (s_instance && s_instance._uiInstance)
            {
                s_instance._uiInstance.Log(msg, type);
            }
            else
            {
                // Fallback to Unity Console if UI is missing
                Debug.LogFormat(
                    logType: type,
                    logOptions: LogOption.None,
                    context: s_instance,
                    format: "{0}(noinst): {1}", nameof(QRCodeManager), msg
                );
            }
        }
    }
}