using UnityEngine;
using VTOLTrainer;

namespace VTOLPayload.Patches
{
    /// Unity's rigidbody integrator runs at 50 Hz by default — that's a 14m step at 700 m/s,
    /// which the solver can't keep up with, hence the "shakes like a hooker in a truck" jitter
    /// above ~2500 km/h. Rather than capping velocity (the previous fix the user vetoed), we
    /// shrink Time.fixedDeltaTime proportionally to the player's airspeed, up to ~125 Hz at
    /// extreme speed. Always on — this is a stability patch, not a cheat.
    internal static class HighSpeedStabilityDriver
    {
        private const float SpeedLowMps  = 500f;   // ~1800 km/h — start ramping
        private const float SpeedHighMps = 800f;   // ~2880 km/h — full clamp
        private const float MinFixedDt   = 0.008f; // 125 Hz ceiling

        private static float _stockFixedDt;
        private static bool _captured;
        private static float _lastApplied;

        public static void Tick()
        {
            var actor = FlightSceneManager.instance ? FlightSceneManager.instance.playerActor : null;
            if (actor == null) { Restore(); return; }

            var rb = actor.GetComponentInParent<Rigidbody>();
            if (rb == null) { Restore(); return; }

            if (!_captured)
            {
                _stockFixedDt = Time.fixedDeltaTime;
                _captured = true;
                _lastApplied = _stockFixedDt;
                Plugin.Log.LogInfo($"HighSpeedStability: captured stock fixedDeltaTime = {_stockFixedDt:0.0000}s ({1f / _stockFixedDt:0} Hz)");
            }

            float speed = rb.velocity.magnitude;
            float t = Mathf.InverseLerp(SpeedLowMps, SpeedHighMps, speed);
            float targetDt = Mathf.Lerp(_stockFixedDt, MinFixedDt, t);

            // Only write when it actually changes meaningfully — avoid thrashing Unity's scheduler.
            if (Mathf.Abs(Time.fixedDeltaTime - targetDt) > 0.0005f)
            {
                Time.fixedDeltaTime = targetDt;
                // Log step transitions so the dev console gives us a breadcrumb when stability kicks in.
                if (Mathf.Abs(targetDt - _lastApplied) > 0.002f)
                {
                    Plugin.Log.LogInfo($"HighSpeedStability: speed={speed:0} m/s → fixedDt={targetDt:0.0000}s ({1f / targetDt:0} Hz)");
                    _lastApplied = targetDt;
                }
            }
        }

        public static void Restore()
        {
            if (_captured) Time.fixedDeltaTime = _stockFixedDt;
            _captured = false;
        }
    }
}
