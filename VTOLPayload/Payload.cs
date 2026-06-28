using HarmonyLib;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VTOLPayload.Patches;
using VTOLPayload.UI;
using VTOLTrainer;

namespace VTOLPayload
{
    public class Payload : IPayload
    {
        public const string Version = "0.1.0";
        private const string HarmonyId = "com.tymek.vtoltrainer.payload";

        private Harmony _harmony;
        private VrCanvasMenu _fallbackMenu;
        private SteamVROverlayMenu _overlay;

        public void Initialize()
        {
            try { new Harmony(HarmonyId).UnpatchSelf(); } catch { }
            _harmony = new Harmony(HarmonyId);

            // Patch each class individually so one bad class can't take down the rest of the suite
            // (e.g., a game-side rename of BlackoutEffect would otherwise also kill FuelTank patching).
            int ok = 0, failed = 0;
            foreach (var type in typeof(Payload).Assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0) continue;
                try
                {
                    _harmony.CreateClassProcessor(type).Patch();
                    Plugin.Log.LogInfo($"  ✓ patched {type.FullName}");
                    ok++;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"  ✗ patch FAILED {type.FullName}: {ex.Message}");
                    failed++;
                }
            }
            Plugin.Log.LogInfo($"Harmony patches: {ok} ok, {failed} failed");

            // Primary VR UI: SteamVR compositor overlay. SteamVR draws this directly onto the HMD
            // frame, bypassing whatever Unity render path was eating our WorldSpace canvas text.
            _overlay = new SteamVROverlayMenu();
            bool overlayOk = false;
            try { overlayOk = _overlay.Initialize(); }
            catch (Exception ex) { Plugin.Log.LogError($"Overlay init threw: {ex.Message}"); }

            if (!overlayOk)
            {
                Plugin.Log.LogWarning("SteamVR overlay unavailable — falling back to WorldSpace canvas.");
                _fallbackMenu = new VrCanvasMenu();
                _fallbackMenu.SetVisible(true);
            }
            Plugin.Log.LogInfo($"VTOL Payload v{Version} initialized");
        }

        public void Shutdown()
        {
            try { _harmony?.UnpatchSelf(); } catch { }
            _harmony = null;
            try { _overlay?.Shutdown(); } catch (Exception ex) { Plugin.Log.LogError($"Overlay shutdown: {ex.Message}"); }
            _overlay = null;
            _fallbackMenu?.Destroy();
            _fallbackMenu = null;
            TimeScaleDriver.ResetToNormal();
            ThrustDriver.ResetToStock();
            NoDragDriver.Restore();
            SuperAirbrakeDriver.Restore();
            WeaponOverdriveDriver.Restore();
            HighSpeedStabilityDriver.Restore();
            Plugin.Log.LogInfo("VTOL Payload shutdown");
        }

        private static void AdjustThrust(float delta)
        {
            float next = Mathf.Clamp(PluginState.ThrustMultiplier + delta, PluginState.ThrustMin, PluginState.ThrustMax);
            // Snap to a multiple of step to keep the display tidy (0.1, 0.2, ...).
            next = Mathf.Round(next / PluginState.ThrustStep) * PluginState.ThrustStep;
            if (!Mathf.Approximately(next, PluginState.ThrustMultiplier))
            {
                PluginState.ThrustMultiplier = next;
                Plugin.Log.LogInfo($"Thrust = {next:0.0}×");
            }
        }

        public void Tick()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f1Key.wasPressedThisFrame)
                {
                    if (_overlay != null && _overlay.Available) _overlay.Toggle();
                    else _fallbackMenu?.Toggle();
                }
                if (kb.f2Key.wasPressedThisFrame)  { PluginState.InfiniteFuel            = !PluginState.InfiniteFuel;            Plugin.Log.LogInfo($"F2 → InfiniteFuel = {PluginState.InfiniteFuel}"); }
                if (kb.f3Key.wasPressedThisFrame)  { PluginState.Invincible              = !PluginState.Invincible;              Plugin.Log.LogInfo($"F3 → Invincible = {PluginState.Invincible}"); }
                if (kb.f4Key.wasPressedThisFrame)  { PluginState.InfiniteWeapons         = !PluginState.InfiniteWeapons;         Plugin.Log.LogInfo($"F4 → WeaponOverdrive = {PluginState.InfiniteWeapons}"); }
                if (kb.f6Key.wasPressedThisFrame)  { PluginState.NoGForce                = !PluginState.NoGForce;                Plugin.Log.LogInfo($"F6 → NoGForce = {PluginState.NoGForce}"); }
                if (kb.f7Key.wasPressedThisFrame)  { PluginState.InfiniteCountermeasures = !PluginState.InfiniteCountermeasures; Plugin.Log.LogInfo($"F7 → InfiniteCountermeasures = {PluginState.InfiniteCountermeasures}"); }
                if (kb.f9Key.wasPressedThisFrame)
                {
                    PluginState.TimeScaleIdx = (PluginState.TimeScaleIdx + 1) % PluginState.TimeScales.Length;
                    Plugin.Log.LogInfo($"F9 → TimeScale = {PluginState.CurrentTimeScale}×");
                }

                // Thrust multiplier: M up, N down, ',' reset to 1.0
                if (kb.mKey.wasPressedThisFrame)     AdjustThrust(+PluginState.ThrustStep);
                if (kb.nKey.wasPressedThisFrame)     AdjustThrust(-PluginState.ThrustStep);
                if (kb.commaKey.wasPressedThisFrame)
                {
                    PluginState.ThrustMultiplier = 1f;
                    Plugin.Log.LogInfo(", → Thrust = 1.0×");
                }

                // Offensive package
                if (kb.leftBracketKey.wasPressedThisFrame)  { PluginState.NoDrag         = !PluginState.NoDrag;         Plugin.Log.LogInfo($"[ → NoDrag = {PluginState.NoDrag}"); }
                if (kb.rightBracketKey.wasPressedThisFrame) { PluginState.SuperAirbrake  = !PluginState.SuperAirbrake;  Plugin.Log.LogInfo($"] → SuperAirbrake = {PluginState.SuperAirbrake}"); }
                if (kb.backslashKey.wasPressedThisFrame)    { PluginState.MissileBoost   = !PluginState.MissileBoost;   Plugin.Log.LogInfo($"\\ → MissileBoost = {PluginState.MissileBoost}"); }
                if (kb.kKey.wasPressedThisFrame)            { PluginState.RequestAutoLockShoot = true;                  Plugin.Log.LogInfo("K → AutoLockShoot requested"); }
            }

            SafeTick("InvincibilityDriver",     InvincibilityDriver.Tick);
            SafeTick("ScenarioFlagDriver",      ScenarioFlagDriver.Tick);
            SafeTick("TimeScaleDriver",         TimeScaleDriver.Tick);
            SafeTick("ThrustDriver",            ThrustDriver.Tick);
            SafeTick("NoDragDriver",            NoDragDriver.Tick);
            SafeTick("SuperAirbrakeDriver",     SuperAirbrakeDriver.Tick);
            SafeTick("WeaponOverdriveDriver",   WeaponOverdriveDriver.Tick);
            SafeTick("AutoLockShootAction",     AutoLockShootAction.Tick);
            SafeTick("HighSpeedStabilityDriver", HighSpeedStabilityDriver.Tick);
            try { _overlay?.Tick(MenuContent.BuildStatusText()); }
            catch (Exception ex) { Plugin.Log.LogError($"Overlay.Tick: {ex.Message}"); }
            try { _fallbackMenu?.Refresh(); }
            catch (Exception ex) { Plugin.Log.LogError($"FallbackMenu.Refresh: {ex.Message}"); }
        }

        private static void SafeTick(string name, Action tick)
        {
            try { tick(); }
            catch (Exception ex) { Plugin.Log.LogError($"{name}.Tick: {ex.Message}"); }
        }

        public void Draw()
        {
            // Desktop-monitor fallback. VR HMD won't see this, but the streamer/observer can.
            try { ImguiOverlay.Draw(); }
            catch (Exception ex) { Plugin.Log.LogError($"ImguiOverlay.Draw: {ex.Message}"); }
        }
    }
}
