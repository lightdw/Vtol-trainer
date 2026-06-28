using UnityEngine;
using VTOLTrainer;

namespace VTOLPayload.UI
{
    /// IMGUI overlay rendered to the desktop monitor window via OnGUI.
    /// VR players can't see this in the headset, but it's a guaranteed-visible fallback
    /// when the WorldSpace canvas is having issues — they can glance at the desktop
    /// monitor or stream to verify toggle state.
    internal static class ImguiOverlay
    {
        private static GUIStyle _box;
        private static GUIStyle _label;

        public static void Draw()
        {
            EnsureStyles();
            var area = new Rect(12, 12, 340, 342);
            GUI.Box(area, "VTOL VR Trainer", _box);

            int y = 38;
            DrawRow(area, ref y, "F2 Fuel",   PluginState.InfiniteFuel);
            DrawRow(area, ref y, "F3 Invuln", PluginState.Invincible);
            DrawRow(area, ref y, "F4 Overdrive", PluginState.InfiniteWeapons);
            DrawRow(area, ref y, "F6 NoG",    PluginState.NoGForce);
            DrawRow(area, ref y, "F7 Cms",    PluginState.InfiniteCountermeasures);
            DrawRow(area, ref y, "[  NoDrag", PluginState.NoDrag);
            DrawRow(area, ref y, "]  SuperBrake", PluginState.SuperAirbrake);
            DrawRow(area, ref y, "\\  MissileBoost", PluginState.MissileBoost);
            DrawTimeRow(area, ref y);
            DrawThrustRow(area, ref y);
            DrawHint(area, ref y, "K AutoShoot");
            DrawHint(area, ref y, $"load #{Plugin.LoadCount}    F5 reload  F1 menu");
        }

        private static void DrawRow(Rect area, ref int y, string label, bool on)
        {
            _label.normal.textColor = on ? new Color(1f, 0.49f, 0.85f, 1f) : new Color(0.55f, 0.5f, 0.58f, 1f);
            GUI.Label(new Rect(area.x + 14, area.y + y, area.width - 28, 22),
                $"{label}: {(on ? "ON" : "off")}", _label);
            y += 22;
        }

        private static void DrawTimeRow(Rect area, ref int y)
        {
            _label.normal.textColor = new Color(1f, 0.82f, 0.95f, 1f);
            GUI.Label(new Rect(area.x + 14, area.y + y, area.width - 28, 22),
                $"F9 TimeScale: {PluginState.CurrentTimeScale:0.##}x", _label);
            y += 22;
        }

        private static void DrawThrustRow(Rect area, ref int y)
        {
            bool active = PluginState.ThrustMultiplier > 1.0001f;
            _label.normal.textColor = active ? new Color(1f, 0.49f, 0.85f, 1f) : new Color(1f, 0.82f, 0.95f, 1f);
            GUI.Label(new Rect(area.x + 14, area.y + y, area.width - 28, 22),
                $"N/M Thrust: {PluginState.ThrustMultiplier:0.0}x", _label);
            y += 22;
        }

        private static void DrawHint(Rect area, ref int y, string text)
        {
            _label.normal.textColor = new Color(0.72f, 0.66f, 0.76f, 1f);
            GUI.Label(new Rect(area.x + 14, area.y + y, area.width - 28, 22), text, _label);
            y += 22;
        }

        private static void EnsureStyles()
        {
            if (_box == null)
            {
                _box = new GUIStyle(GUI.skin.box)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 14,
                    alignment = TextAnchor.UpperCenter
                };
                _box.normal.textColor = new Color(1f, 0.49f, 0.85f, 1f);
            }
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            }
        }
    }
}
