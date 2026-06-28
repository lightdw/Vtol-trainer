using VTOLTrainer;

namespace VTOLPayload.UI
{
    /// Shared between SteamVROverlayMenu and VrCanvasMenu so both UIs show the same text.
    internal static class MenuContent
    {
        public static string BuildStatusText()
        {
            string On  = "<color=#FF7CDD><b>ON</b></color>";
            string Off = "<color=#8C7F95>off</color>";
            string ts  = PluginState.CurrentTimeScale.ToString("0.##") + "x";
            string thr = PluginState.ThrustMultiplier.ToString("0.0") + "x";

            return
                $"<b>[F2]</b>  Infinite Fuel             {(PluginState.InfiniteFuel            ? On : Off)}\n" +
                $"<b>[F3]</b>  Invincibility             {(PluginState.Invincible              ? On : Off)}\n" +
                $"<b>[F4]</b>  Weapon Overdrive          {(PluginState.InfiniteWeapons         ? On : Off)}\n" +
                $"<b>[F6]</b>  No G-LOC                  {(PluginState.NoGForce                ? On : Off)}\n" +
                $"<b>[F7]</b>  Infinite Countermeasures  {(PluginState.InfiniteCountermeasures ? On : Off)}\n" +
                $"<b>[ [ ]</b>   No Drag                   {(PluginState.NoDrag                  ? On : Off)}\n" +
                $"<b>[ ] ]</b>   Super Airbrake            {(PluginState.SuperAirbrake           ? On : Off)}\n" +
                $"<b>[ \\ ]</b>   Missile Boost             {(PluginState.MissileBoost            ? On : Off)}\n" +
                $"<b>[F9]</b>  Time Scale                <color=#FFD0F2><b>{ts}</b></color>\n" +
                $"<b>[N/M/,]</b> Thrust Multiplier         <color=#FFD0F2><b>{thr}</b></color>\n" +
                $"\n" +
                $"<b>[ K ]</b>  Auto-Lock & Shoot (one-shot)\n" +
                $"\n" +
                $"<b>[F1]</b>  Hide/Show menu      <b>[F5]</b> Reload payload (dev)\n" +
                $"<color=#B6A8BE>payload load #{Plugin.LoadCount}</color>";
        }
    }
}
