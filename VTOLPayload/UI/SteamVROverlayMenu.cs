using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Valve.VR;
using VTOLTrainer;

namespace VTOLPayload.UI
{
    /// SteamVR compositor overlay — bypasses Unity's render pipeline entirely.
    /// SteamVR draws this directly onto the HMD frame, so even when VTOL VR's WorldSpace
    /// canvas is broken for us, this overlay still shows. Renders our UI into a
    /// RenderTexture via a private offscreen camera, then hands the native texture handle
    /// to OpenVR.Overlay.SetOverlayTexture each frame.
    internal class SteamVROverlayMenu
    {
        private const string OverlayKey = "com.tymek.vtoltrainer.menu";
        private const int RenderWidth   = 768;
        private const int RenderHeight  = 768;
        private const int UiLayer       = 31;   // hijack the highest layer for our private camera + canvas

        private ulong _handle = OpenVR.k_ulOverlayHandleInvalid;
        private RenderTexture _rt;
        private Camera _renderCam;
        private GameObject _canvasGo;
        private Text _statusText;
        private bool _visible;
        private bool _ready;
        private static Font _cachedFont;

        public bool Available => _ready && _handle != OpenVR.k_ulOverlayHandleInvalid;

        public bool Initialize()
        {
            var overlay = OpenVR.Overlay;
            if (overlay == null)
            {
                Plugin.Log.LogWarning("SteamVR overlay API unavailable (OpenVR.Overlay == null) — falling back to WorldSpace canvas.");
                return false;
            }

            ulong handle = 0;
            var err = overlay.CreateOverlay(OverlayKey, "VTOL VR Trainer", ref handle);
            if (err != EVROverlayError.None)
            {
                Plugin.Log.LogError($"OpenVR CreateOverlay failed: {err}");
                return false;
            }
            _handle = handle;

            overlay.SetOverlayWidthInMeters(_handle, 0.55f);
            overlay.SetOverlayAlpha(_handle, 1.0f);
            overlay.SetOverlayColor(_handle, 1f, 1f, 1f);

            // HMD-relative: 1.0m forward, 0.15m down, identity rotation.
            // HmdMatrix34_t layout: row-major 3 rows × 4 cols (last col = translation).
            var transform = new HmdMatrix34_t
            {
                m0 = 1f, m1 = 0f, m2 = 0f,  m3  = 0f,
                m4 = 0f, m5 = 1f, m6 = 0f,  m7  = -0.15f,
                m8 = 0f, m9 = 0f, m10 = 1f, m11 = -1.0f,
            };
            overlay.SetOverlayTransformTrackedDeviceRelative(_handle, OpenVR.k_unTrackedDeviceIndex_Hmd, ref transform);

            BuildRenderPipeline();
            overlay.ShowOverlay(_handle);
            _visible = true;
            _ready = true;

            Plugin.Log.LogInfo($"SteamVR overlay ready (handle={_handle}). Pipeline: {RenderWidth}×{RenderHeight} → SteamVR compositor.");
            return true;
        }

        public void SetVisible(bool v)
        {
            _visible = v;
            var overlay = OpenVR.Overlay;
            if (overlay == null || _handle == OpenVR.k_ulOverlayHandleInvalid) return;
            if (v) overlay.ShowOverlay(_handle);
            else overlay.HideOverlay(_handle);
        }

        public void Toggle() => SetVisible(!_visible);

        public void Tick(string statusText)
        {
            if (!_ready) return;
            if (_statusText != null) _statusText.text = statusText;

            if (!_visible) return;
            var overlay = OpenVR.Overlay;
            if (overlay == null) return;

            var tex = new Texture_t
            {
                handle = _rt.GetNativeTexturePtr(),
                eType = ETextureType.DirectX,   // VTOL VR uses D3D11 on Windows
                eColorSpace = EColorSpace.Auto
            };
            overlay.SetOverlayTexture(_handle, ref tex);
        }

        public void Shutdown()
        {
            var overlay = OpenVR.Overlay;
            if (overlay != null && _handle != OpenVR.k_ulOverlayHandleInvalid)
            {
                try { overlay.DestroyOverlay(_handle); } catch { }
                _handle = OpenVR.k_ulOverlayHandleInvalid;
            }
            if (_rt != null) { _rt.Release(); UnityEngine.Object.Destroy(_rt); _rt = null; }
            if (_canvasGo != null) { UnityEngine.Object.Destroy(_canvasGo); _canvasGo = null; }
            if (_renderCam != null) { UnityEngine.Object.Destroy(_renderCam.gameObject); _renderCam = null; }
            _statusText = null;
            _ready = false;
        }

        // --- Render pipeline construction ---

        private void BuildRenderPipeline()
        {
            _rt = new RenderTexture(RenderWidth, RenderHeight, 0, RenderTextureFormat.ARGB32)
            {
                name = "VTOLTrainer.OverlayRT"
            };
            _rt.Create();

            var camGo = new GameObject("VTOLTrainer.OverlayCam");
            GameObject.DontDestroyOnLoad(camGo);
            camGo.transform.position = new Vector3(0, -1000, 0); // park far below the scene so nothing else accidentally gets picked up

            _renderCam = camGo.AddComponent<Camera>();
            _renderCam.clearFlags = CameraClearFlags.SolidColor;
            _renderCam.backgroundColor = new Color(0, 0, 0, 0);
            _renderCam.cullingMask = 1 << UiLayer;
            _renderCam.orthographic = true;
            _renderCam.orthographicSize = 5;
            _renderCam.nearClipPlane = 0.1f;
            _renderCam.farClipPlane = 50f;
            _renderCam.targetTexture = _rt;
            _renderCam.depth = -100;
            _renderCam.allowMSAA = false;
            _renderCam.allowHDR = false;
            _renderCam.stereoTargetEye = StereoTargetEyeMask.None;

            _canvasGo = new GameObject("VTOLTrainer.OverlayCanvas");
            GameObject.DontDestroyOnLoad(_canvasGo);
            _canvasGo.transform.SetParent(camGo.transform, false);
            _canvasGo.transform.localPosition = new Vector3(0, 0, 1f);

            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _renderCam;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 32760;

            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            _canvasGo.AddComponent<GraphicRaycaster>();

            BuildContent();
            SetLayerRecursive(_canvasGo, UiLayer);
        }

        private void BuildContent()
        {
            var bg = NewChild("BG", typeof(Image));
            var bgImg = bg.GetComponent<Image>();
            bgImg.color = new Color(0.105f, 0.060f, 0.125f, 0.95f);
            bgImg.raycastTarget = false;
            FillParent(bg);

            var border = NewChild("Border", typeof(Image));
            var borderImg = border.GetComponent<Image>();
            borderImg.color = new Color(1f, 0.31f, 0.75f, 0.95f);
            borderImg.raycastTarget = false;
            FillParent(border, padding: 6);
            border.transform.SetAsFirstSibling();

            var font = GetFont();
            Plugin.Log.LogInfo($"Overlay font: {(font != null ? font.name : "NONE")}");

            // Title
            var titleGo = NewChild("Title", typeof(Text));
            var title = titleGo.GetComponent<Text>();
            if (font != null) title.font = font;
            title.text = "♥  VTOL VR TRAINER  ♥";
            title.fontSize = 30;
            title.alignment = TextAnchor.UpperCenter;
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(1f, 0.48f, 0.87f, 1f);
            title.supportRichText = true;
            title.raycastTarget = false;
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(1, 1);
            titleRt.pivot = new Vector2(0.5f, 1);
            titleRt.anchoredPosition = new Vector2(0, -18);
            titleRt.sizeDelta = new Vector2(0, 50);

            // Status
            var statusGo = NewChild("Status", typeof(Text));
            _statusText = statusGo.GetComponent<Text>();
            if (font != null) _statusText.font = font;
            _statusText.text = "Initializing…";
            _statusText.fontSize = 20;
            _statusText.alignment = TextAnchor.UpperLeft;
            _statusText.fontStyle = FontStyle.Normal;
            _statusText.color = new Color(0.984f, 0.894f, 1f, 1f);
            _statusText.supportRichText = true;
            _statusText.raycastTarget = false;
            _statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _statusText.verticalOverflow = VerticalWrapMode.Overflow;
            var statusRt = statusGo.GetComponent<RectTransform>();
            statusRt.anchorMin = Vector2.zero; statusRt.anchorMax = Vector2.one;
            statusRt.offsetMin = new Vector2(32, 28);
            statusRt.offsetMax = new Vector2(-32, -78);
        }

        private GameObject NewChild(string name, params Type[] components)
        {
            var go = new GameObject(name, components);
            go.transform.SetParent(_canvasGo.transform, false);
            return go;
        }

        private static void FillParent(GameObject go, int padding = 0)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-padding, -padding);
            rt.offsetMax = new Vector2( padding,  padding);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
        }

        private static Font GetFont()
        {
            if (_cachedFont != null) return _cachedFont;
            var all = Resources.FindObjectsOfTypeAll<Font>().Where(f => f != null).ToList();
            string[] preferred = { "arial", "liberation", "sans", "roboto", "noto", "dejavu" };
            foreach (var name in preferred)
            {
                var match = all.FirstOrDefault(f => f.name != null && f.name.ToLowerInvariant().Contains(name));
                if (match != null) { _cachedFont = match; return _cachedFont; }
            }
            _cachedFont = all.FirstOrDefault();
            return _cachedFont;
        }
    }
}
