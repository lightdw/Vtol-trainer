using HarmonyLib;
using VTOLTrainer;

namespace VTOLPayload.Patches
{
    /// Multiplies a player-launched missile's speed / turn-rate / prox-fuse on spawn.
    /// We hook Missile.Start so we run *after* the missile has read its own config but
    /// before it begins flying. We refuse to buff anything not launched by the player's
    /// team — otherwise incoming SAMs would also become un-dodgeable.
    [HarmonyPatch(typeof(Missile), "Start")]
    internal static class MissileBoost_StartPatch
    {
        // VTOL VR has revised these field names a few times across versions, so we hunt.
        private static readonly string[] SpeedFields = { "maxSpeed", "topSpeed" };
        private static readonly string[] AccelFields = { "maxAcceleration", "maxAccel", "acceleration" };
        private static readonly string[] TurnFields  = { "maxTurnSpeed", "turnSpeed", "turnRate", "maxTurnRate" };
        private static readonly string[] ProxFields  = { "proxDistance", "proxFuseRange", "proximityFuseRange", "proxRadius" };

        [HarmonyPostfix]
        private static void Postfix(Missile __instance)
        {
            try
            {
                if (!PluginState.MissileBoost || __instance == null) return;
                if (!IsPlayerLaunched(__instance)) return;

                var tr = Traverse.Create(__instance);
                int hits = 0;
                hits += ScaleFirst(tr, SpeedFields, PluginState.MissileSpeedMult, "speed");
                hits += ScaleFirst(tr, AccelFields, PluginState.MissileSpeedMult, "accel");
                hits += ScaleFirst(tr, TurnFields,  PluginState.MissileTurnMult,  "turn");
                hits += ScaleFirst(tr, ProxFields,  PluginState.MissileProxMult,  "prox");

                if (hits > 0)
                    Plugin.Log.LogInfo($"MissileBoost: buffed {__instance.GetType().Name} ({hits} field(s))");
                else
                    Plugin.Log.LogWarning("MissileBoost: no recognised speed/turn/prox fields on missile.");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"MissileBoost.Postfix: {ex.Message}");
            }
        }

        private static int ScaleFirst(Traverse tr, string[] candidates, float mult, string tag)
        {
            foreach (var name in candidates)
            {
                var f = tr.Field(name);
                if (!f.FieldExists()) continue;
                if (f.GetValueType() != typeof(float)) continue;
                float baseVal = f.GetValue<float>();
                f.SetValue(baseVal * mult);
                Plugin.Log.LogInfo($"  · {tag}: {name} {baseVal:0.##} → {baseVal * mult:0.##}");
                return 1;
            }
            return 0;
        }

        private static bool IsPlayerLaunched(Missile m)
        {
            var actor = FlightSceneManager.instance ? FlightSceneManager.instance.playerActor : null;
            if (actor == null) return false;

            // Probe a few common "who fired me" field names.
            var tr = Traverse.Create(m);
            foreach (var name in new[] { "launchingActor", "launcher", "owner", "team" })
            {
                var f = tr.Field(name);
                if (!f.FieldExists()) continue;
                var val = f.GetValue();
                if (val == null) continue;

                if (val is Actor a) return a == actor || a.team == actor.team;
                // Team enum directly on the missile
                if (val.GetType().IsEnum) return val.ToString() == actor.team.ToString();
            }
            // Fall back to false — better to no-op than buff hostile missiles.
            return false;
        }
    }
}
