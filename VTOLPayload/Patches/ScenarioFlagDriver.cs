using VTOLTrainer;

namespace VTOLPayload.Patches
{
    /// Drives VTScenario.current.infiniteAmmo from PluginState. The game has a built-in
    /// InfReloadRoutine that reloads all missiles when count hits 0 and infiniteAmmo is true.
    internal static class ScenarioFlagDriver
    {
        // Remember whether the scenario originally had infiniteAmmo on, so when the user
        // toggles F4 off we restore that value instead of force-disabling it.
        private static VTScenario _trackedScenario;
        private static bool _originalInfiniteAmmo;

        public static void Tick()
        {
            var scenario = VTScenario.current;
            if (scenario == null) { _trackedScenario = null; return; }

            if (!ReferenceEquals(scenario, _trackedScenario))
            {
                _trackedScenario = scenario;
                _originalInfiniteAmmo = scenario.infiniteAmmo;
            }

            bool desired = PluginState.InfiniteWeapons || _originalInfiniteAmmo;
            if (scenario.infiniteAmmo != desired) scenario.infiniteAmmo = desired;
        }
    }
}
