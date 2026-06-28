using HarmonyLib;
using VTOLTrainer;

namespace VTOLPayload.Patches
{
    /// VehiclePart.GUpdateRoutine accumulates G-stress and, when it overloads, calls
    /// Health.Damage(... "G-Force Damage" ...) which damages or rips off pylons + weapons.
    /// While NoGForce is on, swallow those specific damage calls — keeps missiles
    /// glued to the rails during hard maneuvers.
    [HarmonyPatch(typeof(Health), nameof(Health.Damage))]
    internal static class HealthDamage_GForcePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(string message)
        {
            if (!PluginState.NoGForce) return true;
            if (message == "G-Force Damage") return false;
            return true;
        }
    }
}
