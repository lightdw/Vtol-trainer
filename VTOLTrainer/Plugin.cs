using BepInEx;
using BepInEx.Logging;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace VTOLTrainer
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.tymek.vtoltrainer";
        public const string PluginName = "VTOL VR Trainer";
        public const string PluginVersion = "0.3.0";

        public static ManualLogSource Log { get; private set; }
        public static Plugin Instance { get; private set; }
        public static int LoadCount { get; private set; }

        private IPayload _payload;
        private string _payloadPath;
        private Driver _driver;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            var dir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
            _payloadPath = Path.Combine(dir, "VTOLPayload.dll");

            Log.LogInfo($"{PluginName} {PluginVersion} (shim) loaded");
            Log.LogInfo($"Payload path: {_payloadPath}");
            Log.LogInfo("Hotkeys: F1=menu  F2=Fuel  F3=Invuln  F4=Weapons  F5=Reload  F6=NoG  F7=Cms  F8=Repair  F9=TimeScale  F10=TP-Waypoint  F11=KillEnemies  [=NoDrag  ]=SuperBrake  \\=MissileBoost  K=AutoLockShoot");

            // Spin our own persistent GameObject so we don't depend on BepInEx's manager
            // GO surviving scene transitions (VTOL VR seems to strip it).
            var go = new GameObject("VTOLTrainer.Driver");
            GameObject.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _driver = go.AddComponent<Driver>();
            _driver.Init(this);

            SceneManager.sceneLoaded += OnSceneLoaded;

            LoadPayload();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log.LogInfo($"Scene loaded: {scene.name} (mode={mode})  driver alive={(_driver != null)}");
        }

        internal void DriverUpdate()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f5Key.wasPressedThisFrame) RequestReload();

            if (_reloadRequested)
            {
                _reloadRequested = false;
                LoadPayload();
                return;
            }

            try { _payload?.Tick(); }
            catch (Exception ex) { Log.LogError($"Payload.Tick: {ex}"); }
        }

        internal void DriverOnGUI()
        {
            try { _payload?.Draw(); }
            catch (Exception ex) { Log.LogError($"Payload.Draw: {ex}"); }
        }

        private bool _reloadRequested;
        public static void RequestReload()
        {
            if (Instance != null) Instance._reloadRequested = true;
        }

        private void LoadPayload()
        {
            if (_payload != null)
            {
                try { _payload.Shutdown(); }
                catch (Exception ex) { Log.LogError($"Payload.Shutdown: {ex}"); }
                _payload = null;
            }

            if (!File.Exists(_payloadPath))
            {
                Log.LogError($"Payload not found: {_payloadPath}");
                return;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(_payloadPath);
                Assembly asm = Assembly.Load(bytes);
                Type type = asm.GetType("VTOLPayload.Payload");
                if (type == null)
                {
                    Log.LogError("VTOLPayload.Payload type not found");
                    return;
                }
                _payload = (IPayload)Activator.CreateInstance(type);
                _payload.Initialize();
                LoadCount++;
                Log.LogInfo($"Loaded payload #{LoadCount} ({bytes.Length} bytes)");
            }
            catch (Exception ex)
            {
                Log.LogError($"Payload load failed: {ex}");
            }
        }
    }

    internal class Driver : MonoBehaviour
    {
        private Plugin _plugin;
        private int _heartbeat;

        public void Init(Plugin plugin)
        {
            _plugin = plugin;
            Plugin.Log.LogInfo("Driver MonoBehaviour Awake — Update will run here");
        }

        private void Update()
        {
            if (_heartbeat >= 0 && ++_heartbeat == 60)
            {
                Plugin.Log.LogInfo($"Driver.Update heartbeat (frame 60). Keyboard.current = {(Keyboard.current != null ? "OK" : "NULL")}");
                _heartbeat = -1;
            }
            _plugin?.DriverUpdate();
        }

        private void OnGUI() => _plugin?.DriverOnGUI();

        private void OnDestroy()
        {
            Plugin.Log.LogWarning("Driver GameObject was destroyed — Update will stop firing!");
        }
    }

    public interface IPayload
    {
        void Initialize();
        void Shutdown();
        void Tick();
        void Draw();
    }

    public static class PluginState
    {
        // Toggles (set/cleared by F-keys)
        public static bool InfiniteFuel            = false;
        public static bool InfiniteWeapons         = false;
        public static bool Invincible              = false;
        public static bool NoGForce                = false;
        public static bool InfiniteCountermeasures = false;
        public static bool NoDrag                  = false;
        public static bool SuperAirbrake           = false;
        public static bool MissileBoost            = false;

        // How much SuperAirbrake multiplies airbrake drag. 8× picked empirically as
        // "stops you fast but doesn't snap your neck."
        public const float SuperAirbrakeMultiplier = 8.0f;
        // Missile boost multipliers — applied at spawn time to player-launched missiles only.
        public const float MissileSpeedMult  = 2.0f;
        public const float MissileTurnMult   = 3.0f;
        public const float MissileProxMult   = 2.5f;

        // Time scale: index into TimeScales; 1.0× is the default and is index 1.
        public static readonly float[] TimeScales = new[] { 0.5f, 1f, 2f, 4f };
        public static int TimeScaleIdx = 1;
        public static float CurrentTimeScale => TimeScales[TimeScaleIdx];

        // Thrust multiplier: clamped [1, 50] in 1.0 steps. 1 = stock.
        public const float ThrustMin = 1.0f;
        public const float ThrustMax = 50.0f;
        public const float ThrustStep = 1.0f;
        public static float ThrustMultiplier = 1.0f;

        // One-shot request flags — set true by a key press, consumed by the corresponding driver.
        public static bool RequestRepair           = false;
        public static bool RequestTeleportWaypoint = false;
        public static bool RequestKillHostiles     = false;
        public static bool RequestAutoLockShoot    = false;
    }
}
