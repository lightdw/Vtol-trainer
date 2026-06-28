using HarmonyLib;
using VTOLTrainer;

namespace VTOLPayload.Patches
{
    [HarmonyPatch(typeof(FuelTank), nameof(FuelTank.RequestFuel), new[] { typeof(double) })]
    internal static class FuelTankRequestFuelPatch
    {
        private static bool _everCalled;
        private static bool _everBlocked;

        [HarmonyPrefix]
        private static bool Prefix(ref float __result, double __0)
        {
            if (!_everCalled)
            {
                _everCalled = true;
                Plugin.Log.LogInfo($"[FuelTank] RequestFuel intercepted (first hit). amount={__0}  InfiniteFuel={PluginState.InfiniteFuel}");
            }
            if (!PluginState.InfiniteFuel) return true;
            if (!_everBlocked)
            {
                _everBlocked = true;
                Plugin.Log.LogInfo($"[FuelTank] Blocking drain (first time toggle was ON). amount={__0}");
            }
            // RequestFuel returns a normalized success RATIO (0-1), not an absolute amount.
            // 1f means "100% of request was delivered" — confirmed by user, this value worked.
            // Returning (float)__0 was wrong: it fed the requested amount back as ratio, which
            // for typical small per-frame requests collapses thrust to ~0.
            __result = 1f;
            return false;
        }
    }
}
