using UnityEngine;
using VTOLTrainer;

namespace VTOLPayload.Patches
{
    /// While NoDrag is on, force the player rigidbody's linear drag (and angular damping)
    /// to zero. Restores the stock values the moment the toggle is flipped off.
    /// Pairs well with the thrust multiplier — the jet keeps every Joule of KE you give it.
    internal static class NoDragDriver
    {
        private static Rigidbody _trackedRb;
        private static float _stockDrag;
        private static float _stockAngularDrag;
        private static bool _stockCaptured;

        public static void Tick()
        {
            var actor = FlightSceneManager.instance ? FlightSceneManager.instance.playerActor : null;
            if (actor == null) { Restore(); return; }

            var rb = actor.GetComponentInParent<Rigidbody>();
            if (rb == null) { Restore(); return; }

            // Player swap (rare, but possible if scenario reloaded) — drop old cache.
            if (_trackedRb != rb)
            {
                Restore();
                _trackedRb = rb;
                _stockDrag = rb.drag;
                _stockAngularDrag = rb.angularDrag;
                _stockCaptured = true;
            }

            if (PluginState.NoDrag)
            {
                if (rb.drag != 0f) rb.drag = 0f;
                if (rb.angularDrag != 0f) rb.angularDrag = 0f;
            }
            else if (_stockCaptured)
            {
                if (rb.drag != _stockDrag) rb.drag = _stockDrag;
                if (rb.angularDrag != _stockAngularDrag) rb.angularDrag = _stockAngularDrag;
            }
        }

        public static void Restore()
        {
            if (_stockCaptured && _trackedRb != null)
            {
                _trackedRb.drag = _stockDrag;
                _trackedRb.angularDrag = _stockAngularDrag;
            }
            _trackedRb = null;
            _stockCaptured = false;
        }
    }
}
