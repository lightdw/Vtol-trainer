using System.Collections.Generic;
using UnityEngine;
using VTOLTrainer;

namespace VTOLPayload.Patches
{
    /// Scales ModuleEngine.maxThrust on every engine in the scene by PluginState.ThrustMultiplier.
    /// We cache each engine's stock maxThrust the first time we see it, so the multiplier is
    /// idempotent — going 1.0 → 2.0 → 1.0 returns exactly to stock without compounding.
    internal static class ThrustDriver
    {
        private static readonly Dictionary<ModuleEngine, float> _baseMaxThrust = new Dictionary<ModuleEngine, float>();
        private static readonly List<ModuleEngine> _scratch = new List<ModuleEngine>();
        private static float _lastApplied = 1.0f;

        public static void Tick()
        {
            float mult = PluginState.ThrustMultiplier;
            // No-op when multiplier is exactly stock and was stock last tick — saves a scene scan.
            if (Mathf.Approximately(mult, 1f) && Mathf.Approximately(_lastApplied, 1f) && _baseMaxThrust.Count == 0) return;

            _scratch.Clear();
            _scratch.AddRange(Object.FindObjectsOfType<ModuleEngine>());

            foreach (var eng in _scratch)
            {
                if (eng == null) continue;
                if (!_baseMaxThrust.TryGetValue(eng, out float baseThrust))
                {
                    baseThrust = eng.maxThrust;
                    _baseMaxThrust[eng] = baseThrust;
                }
                eng.maxThrust = baseThrust * mult;
            }

            if (!Mathf.Approximately(mult, _lastApplied))
            {
                Plugin.Log.LogInfo($"ThrustMultiplier: {mult:0.0}× applied to {_scratch.Count} engines");
                _lastApplied = mult;
            }
        }

        public static void ResetToStock()
        {
            foreach (var kv in _baseMaxThrust)
                if (kv.Key != null) kv.Key.maxThrust = kv.Value;
            _baseMaxThrust.Clear();
            _lastApplied = 1f;
        }
    }
}
