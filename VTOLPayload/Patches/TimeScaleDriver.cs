using UnityEngine;
using VTOLTrainer;

namespace VTOLPayload.Patches
{
    /// Pushes PluginState.TimeScaleIdx → Time.timeScale. Also restores 1.0 on payload shutdown.
    internal static class TimeScaleDriver
    {
        private static float _lastApplied = float.NaN;

        public static void Tick()
        {
            float desired = PluginState.CurrentTimeScale;
            if (!Mathf.Approximately(desired, _lastApplied))
            {
                Time.timeScale = desired;
                _lastApplied = desired;
                Plugin.Log.LogInfo($"TimeScale: {desired:0.##}×");
            }
        }

        public static void ResetToNormal()
        {
            Time.timeScale = 1f;
            _lastApplied = 1f;
        }
    }
}
