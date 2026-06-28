using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using VTOLTrainer;

namespace VTOLPayload.UI
{
    /// World-space VR menu, legacy UI.Text only.
    /// We dropped TextMeshPro because TMP's SDF shader has been inconsistently rendering
    /// in this game's WorldSpace canvas pipeline — legacy Text uses Unity's built-in dynamic
    /// font system, which is the most reliable path across VR rendering modes.
    internal class VrCanvasMenu
    {
        private GameObject _root;
        private Text _statusText;
        private bool _visible;
        private static Font _cachedFont;

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

        public void SetVisible(bool v)
        {
            _visible = v;
            if (v) EnsureBuilt();
            if (_root != null) _root.SetActive(v);
            if (v) AnchorInFrontOfCamera();
        }

        public void Toggle()
        {
            bool reallyVisible = _visible && _root != null;
            SetVisible(!reallyVisible);
        }

        public void Refresh()
        {
            if (!_visible || _statusText == null) return;
            _statusText.text = MenuContent.BuildStatusText();
        }

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
            _statusText = null;
            _visible = false;
        }

        private void EnsureBuilt()
        {
            if (_root != null) return;

            _root = new GameObject("VTOLTrainer.VrCanvas");
            Object.DontDestroyOnLoad(_root);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;       // some Unity / VR paths need this for WorldSpace canvases
            canvas.sortingOrder = 32760;
            _root.AddComponent<CanvasScaler>();
            _root.AddComponent<GraphicRaycaster>();

            var rt = _root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(640, 620);
            rt.localScale = Vector3.one * 0.001f;

            // Background
            var bg = AddImageChild("BG", new Color(0.105f, 0.060f, 0.125f, 0.94f), raycastTarget: false);
            FillParent(bg);

            // Pink border (sits behind BG, offset outward, so an 8px frame shows around the edges)
            var border = AddImageChild("Border", new Color(1f, 0.31f, 0.75f, 0.9f), raycastTarget: false);
            FillParent(border, padding: 4);
            border.transform.SetAsFirstSibling();

            var font = GetFont();
            Plugin.Log.LogInfo($"VrCanvas built (legacy UI.Text). Font: {(font != null ? font.name : "NONE")}");

            // Title
            BuildText("Title", font, "*  VTOL VR TRAINER  *", 34, TextAnchor.UpperCenter, FontStyle.Bold,
                new Color(1f, 0.48f, 0.87f, 1f),
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                anchoredPos: new Vector2(0, -16), sizeDelta: new Vector2(0, 60),
                outRef: out _);

            // Status block
            BuildText("Status", font, MenuContent.BuildStatusText(), 22, TextAnchor.UpperLeft, FontStyle.Normal,
                new Color(0.984f, 0.894f, 1f, 1f),
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
                offsetMin: new Vector2(32, 28), offsetMax: new Vector2(-32, -88),
                outRef: out _statusText);
        }

        private Image AddImageChild(string name, Color color, bool raycastTarget)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_root.transform, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = raycastTarget;
            return img;
        }

        private static void FillParent(Image img, int padding = 0)
        {
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-padding, -padding);
            rt.offsetMax = new Vector2( padding,  padding);
        }

        private void BuildText(string name, Font font, string text, int size, TextAnchor align, FontStyle style, Color color,
            Vector2 amin, Vector2 amax, Vector2 pivot,
            out Text outRef,
            Vector2 anchoredPos = default, Vector2 sizeDelta = default,
            Vector2? offsetMin = null, Vector2? offsetMax = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_root.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = amin; rt.anchorMax = amax; rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeDelta;
            if (offsetMin.HasValue) rt.offsetMin = offsetMin.Value;
            if (offsetMax.HasValue) rt.offsetMax = offsetMax.Value;
            outRef = go.GetComponent<Text>();
            if (font != null) outRef.font = font;
            outRef.text = text;
            outRef.fontSize = size;
            outRef.alignment = align;
            outRef.fontStyle = style;
            outRef.color = color;
            outRef.supportRichText = true;
            outRef.raycastTarget = false;
            outRef.horizontalOverflow = HorizontalWrapMode.Wrap;
            outRef.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private void AnchorInFrontOfCamera()
        {
            var cam = Camera.main;
            if (cam == null || _root == null) return;
            _root.transform.position = cam.transform.position + cam.transform.forward * 1.2f;
            var lookDir = _root.transform.position - cam.transform.position;
            if (lookDir.sqrMagnitude < 1e-6f) lookDir = cam.transform.forward;
            _root.transform.rotation = Quaternion.LookRotation(lookDir);
        }

    }
}
