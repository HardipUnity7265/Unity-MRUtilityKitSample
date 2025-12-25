// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.MRUtilityKit;
using Meta.XR.Samples;
using System;
using System.Collections.Generic; // Added for Dictionary
using UnityEngine;

namespace Meta.XR.MRUtilityKitSamples.QRCodeDetection
{
    [MetaCodeSample("MRUKSample-QRCodeDetection")]
    public class QRCodeManager : MonoBehaviour
    {
        // ------------- Static Interface (Unchanged) -------------
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

        [Tooltip("Your World Space UI Prefab containing the Bracket Image")]
        [SerializeField] private GameObject _qrCodePrefab; 

        [SerializeField] private MRUK _mrukInstance;

        // ------------- Private Fields -------------

        private int _activeCount;
        private static QRCodeManager s_instance;

        // Dictionary to keep track of active QR codes and their visual instances
        private Dictionary<MRUKTrackable, RectTransform> _activeQRVisuals = new Dictionary<MRUKTrackable, RectTransform>();

        // ------------- MonoBehaviour Messages -------------

        void OnValidate()
        {
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
                Debug.LogError($"{nameof(QRCodeManager)} requires an MRUK object in the scene!");
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
            // Iterate through all active QR codes to update their size
            foreach (var kvp in _activeQRVisuals)
            {
                MRUKTrackable trackable = kvp.Key;
                RectTransform visualRect = kvp.Value;

                if (trackable != null && visualRect != null && trackable.PlaneRect.HasValue)
                {
                    // 1. Get Physical Size
                    float width = trackable.PlaneRect.Value.width;
                    float height = trackable.PlaneRect.Value.height;

                    // 2. Apply Scale to the UI
                    // We change localScale so the Image stretches to fit the QR code exactly.
                    // This assumes your Prefab's RectTransform defaults to a 1x1 size (or you want to multiply its size).
                    // If you want to use Slice (9-slicing), change 'sizeDelta' instead of 'localScale'.
                    
                    // APPROACH A: Scaling (Behaves like the Quad - easiest for general "fit")
                     visualRect.localScale = new Vector3(width, height, 1f);

                    // APPROACH B: Sizing (Enable this instead if you use 9-Sliced Images and want crisp corners)
                    // Note: This requires your World Space Canvas scale to be known. 
                    // visualRect.sizeDelta = new Vector2(width / visualRect.lossyScale.x, height / visualRect.lossyScale.y);
                }
            }
        }

        // ------------- UnityEvent Listeners -------------

        public void OnTrackableAdded(MRUKTrackable trackable)
        {
            if (trackable.TrackableType != OVRAnchor.TrackableType.QRCode) return;

            // Instantiate your UI Prefab as a child of the Trackable
            GameObject instance = Instantiate(_qrCodePrefab, trackable.transform);

            // Ensure it's centered
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            // Try to find the RectTransform (Canvas or Image)
            RectTransform rect = instance.GetComponent<RectTransform>();
            if (rect == null)
            {
                // Fallback if the root object isn't the UI element
                rect = instance.GetComponentInChildren<RectTransform>();
            }

            if (rect != null)
            {
                _activeQRVisuals.Add(trackable, rect);
            }

            _activeCount++;
        }

        public void OnTrackableRemoved(MRUKTrackable trackable)
        {
            if (trackable.TrackableType != OVRAnchor.TrackableType.QRCode) return;

            if (_activeQRVisuals.ContainsKey(trackable))
            {
                _activeQRVisuals.Remove(trackable);
            }

            _activeCount--;
            // MRUK automatically destroys the trackable GameObject, which destroys our child instance too.
        }
    }
}