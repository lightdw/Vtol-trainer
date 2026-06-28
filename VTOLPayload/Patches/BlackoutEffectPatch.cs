using HarmonyLib;
using VTOLTrainer;

namespace VTOLPayload.Patches
{
    /// Disables G-LOC: skips FixedUpdate (G-accumulation), blocks AccelDie, and zeroes gAccum.
    [HarmonyPatch(typeof(BlackoutEffect))]
    internal static class BlackoutEffectPatch
    {
        // FixedUpdate is what *accumulates* G-stress over time. Skip it entirely while NoGForce is on,
        // so the accumulator stays at 0 and the player never reaches blackout/redout thresholds.
        [HarmonyPrefix]
        [HarmonyPatch("FixedUpdate")]
        private static bool SkipFixedUpdate(BlackoutEffect __instance)
        {
            if (!PluginState.NoGForce) return true;
            __instance.SetGAccum(0f);
            return false;
        }

        // AccelDie is the death call. Always block it while NoGForce is on, even if some other
        // path bypassed our FixedUpdate prefix.
        [HarmonyPrefix]
        [HarmonyPatch(nameof(BlackoutEffect.AccelDie))]
        private static bool SkipAccelDie() => !PluginState.NoGForce;

        // LateUpdate drives the visual vignette / audio cues from the current gAccum value.
        // Don't skip it — that strands the last-frame tint/audio state on screen. Instead force
        // the accumulator to 0 first, then let LateUpdate run normally and decay the effect.
        [HarmonyPrefix]
        [HarmonyPatch("LateUpdate")]
        private static void ZeroAccumBeforeLateUpdate(BlackoutEffect __instance)
        {
            if (PluginState.NoGForce) __instance.SetGAccum(0f);
        }
    }
}
