using UnityEngine;
using VTOLTrainer;

namespace VTOLPayload.Patches
{
    /// Clamps the player aircraft's rigidbody velocity to a safe ceiling. Unity's rigidbody
    /// integrator starts blowing up around 2200 km/h (jitter / vertical bouncing / collision
    /// tunneling), which the trainer can easily provoke with thrust multiplier + infinite fuel.
    /// We cap at 2100 km/h ≈ 583 m/s — well above any stock vehicle's natural top speed,
    /// but below the physics-instability threshold.
    internal static class VelocityCapDriver
    {
        private const float MaxSpeedMps = 583f; // 2100 km/h

        public static void Tick()
        {
            var actor = FlightSceneManager.instance ? FlightSceneManager.instance.playerActor : null;
            if (actor == null) return;

            var rb = actor.GetComponentInParent<Rigidbody>();
            if (rb == null) return;

            var v = rb.velocity;
            float speed = v.magnitude;
            if (speed > MaxSpeedMps)
            {
                rb.velocity = v * (MaxSpeedMps / speed);
            }
        }
    }
}
