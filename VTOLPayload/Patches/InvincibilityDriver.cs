using System.Collections.Generic;
using UnityEngine;
using VTOLTrainer;

namespace VTOLPayload.Patches
{
    /// Drives Health.invincible on every Health under the local player's actor each frame.
    internal static class InvincibilityDriver
    {
        private static readonly List<Health> _scratch = new List<Health>();
        private static bool _wasOn;

        public static void Tick()
        {
            var actor = FlightSceneManager.instance ? FlightSceneManager.instance.playerActor : null;
            if (actor == null) { _wasOn = false; return; }

            actor.GetComponentsInChildren(true, _scratch);
            bool on = PluginState.Invincible;
            for (int i = 0; i < _scratch.Count; i++)
                _scratch[i].invincible = on;

            if (on != _wasOn)
            {
                _wasOn = on;
                Plugin.Log.LogInfo($"Invincibility {(on ? "ON" : "OFF")} — {_scratch.Count} Health components");
            }
        }
    }
}
