using HarmonyLib;
using VTOLTrainer;

namespace VTOLPayload.Patches
{
    /// Pretends every countermeasure fire was successful without decrementing the count.
    /// Acts on every Countermeasure in the scene including enemies — that's fine, the player
    /// is the one with hotkey access, and AI flares are already infinite-ish from spawn.
    [HarmonyPatch(typeof(Countermeasure), nameof(Countermeasure.ConsumeCM))]
    internal static class CountermeasureConsumeCMPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ref bool __result)
        {
            if (!PluginState.InfiniteCountermeasures) return true;
            __result = true;
            return false;
        }
    }
}
