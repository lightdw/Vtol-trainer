using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using VTOLTrainer;

namespace VTOLPayload.Patches
{
    /// Locates any component on the player vehicle whose type name contains "Airbrake"
    /// (or "AirBrake") and scales the first float field that looks like a drag coefficient
    /// by PluginState.SuperAirbrakeMultiplier. Reflection-driven so we survive minor
    /// VTOL VR refactors without recompiling.
    internal static class SuperAirbrakeDriver
    {
        private struct Tracked
        {
            public Component Comp;
            public System.Reflection.FieldInfo Field;
            public float Base;
        }

        // Field-name candidates we try, in priority order.
        private static readonly string[] FieldCandidates = new[]
        {
            "drag", "dragAmount", "dragCoef", "dragCoefficient",
            "maxDrag", "airbrakeDrag", "brakeDrag"
        };

        private static readonly List<Tracked> _tracked = new List<Tracked>();
        private static bool _searched;
        private static GameObject _searchedFor;
        private static bool _applied;

        public static void Tick()
        {
            var actor = FlightSceneManager.instance ? FlightSceneManager.instance.playerActor : null;
            if (actor == null) { Restore(); _searched = false; _searchedFor = null; return; }

            // Re-scan if the player vehicle changed.
            if (!_searched || _searchedFor != actor.gameObject)
            {
                Restore();
                FindAirbrakes(actor.gameObject);
                _searched = true;
                _searchedFor = actor.gameObject;
            }

            bool wantOn = PluginState.SuperAirbrake;
            if (wantOn == _applied) return;

            foreach (var t in _tracked)
            {
                if (t.Comp == null) continue;
                float val = wantOn ? t.Base * PluginState.SuperAirbrakeMultiplier : t.Base;
                t.Field.SetValue(t.Comp, val);
            }
            _applied = wantOn;
            Plugin.Log.LogInfo($"SuperAirbrake {(wantOn ? "ON" : "off")} — wrote {_tracked.Count} field(s)");
        }

        private static void FindAirbrakes(GameObject root)
        {
            foreach (var comp in root.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                var typeName = comp.GetType().Name;
                if (typeName.IndexOf("airbrake", System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                foreach (var fname in FieldCandidates)
                {
                    var field = AccessTools.Field(comp.GetType(), fname);
                    if (field == null || field.FieldType != typeof(float)) continue;
                    float baseVal = (float)field.GetValue(comp);
                    _tracked.Add(new Tracked { Comp = comp, Field = field, Base = baseVal });
                    Plugin.Log.LogInfo($"SuperAirbrake: tracking {typeName}.{fname} (base={baseVal:0.##})");
                    break; // one field per component is enough
                }
            }
            if (_tracked.Count == 0)
                Plugin.Log.LogWarning("SuperAirbrake: no Airbrake components with a drag-like field were found.");
        }

        public static void Restore()
        {
            foreach (var t in _tracked)
            {
                if (t.Comp != null && t.Field != null)
                    t.Field.SetValue(t.Comp, t.Base);
            }
            _tracked.Clear();
            _applied = false;
        }
    }
}
