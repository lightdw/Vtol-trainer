using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using VTOLTrainer;

namespace VTOLPayload.Patches
{
    internal static class WeaponOverdriveDriver
    {
        private const float GunTargetRpm = 30000f; // 500 rounds/sec

        private static readonly string[] GunTypeHints =
            { "gun", "cannon", "vulcan", "gau", "gatling", "autocannon", "minigun" };

        private static readonly string[] WeaponTypeHints =
            { "weapon", "missile", "launcher", "hardpoint", "pylon", "equip", "bomb", "rocket" };

        private static readonly string[] RpmFields =
            { "rpm", "fireRate", "roundsPerMinute", "cycleRate", "rateOfFire" };

        private static readonly string[] IntervalFields =
            { "timeBetweenShots", "fireInterval", "shotInterval", "fireDelay", "timeBetweenRounds" };

        private static readonly string[] CooldownFields =
            { "cooldownTimer", "cooldown", "reloadTimer", "reloadTime",
              "fireTimer", "lastFireTime", "rearmTimer", "rearmTime",
              "missileReloadTime", "reloadDelay", "rippleTimer" };

        private struct Tracked
        {
            public Component Comp;
            public FieldInfo Field;
            public float Stock;
            public string Tag; // "rpm", "interval", "cooldown"
        }

        private static readonly List<Tracked> _tracked = new List<Tracked>();
        private static Actor _trackedActor;
        private static bool _wasOn;

        public static void Tick()
        {
            if (!PluginState.InfiniteWeapons)
            {
                if (_wasOn) { Restore(); _wasOn = false; }
                return;
            }

            var actor = FlightSceneManager.instance
                ? FlightSceneManager.instance.playerActor : null;
            if (actor == null) { Restore(); return; }

            if (!ReferenceEquals(actor, _trackedActor) || !_wasOn)
            {
                Restore();
                _trackedActor = actor;
                Scan(actor);
                _wasOn = true;
            }

            foreach (var t in _tracked)
            {
                if (t.Comp == null) continue;
                try
                {
                    switch (t.Tag)
                    {
                        case "rpm":
                            field_Set(t, GunTargetRpm);
                            break;
                        case "interval":
                            field_Set(t, 0.0001f);
                            break;
                        case "cooldown":
                            field_Set(t, 0f);
                            break;
                    }
                }
                catch { }
            }
        }

        private static void field_Set(Tracked t, float value)
        {
            if (t.Field.FieldType == typeof(float))
                t.Field.SetValue(t.Comp, value);
            else if (t.Field.FieldType == typeof(double))
                t.Field.SetValue(t.Comp, (double)value);
        }

        private static void Scan(Actor actor)
        {
            var comps = actor.gameObject.GetComponentsInChildren<Component>(true);
            int gunHits = 0, weaponHits = 0;

            foreach (var c in comps)
            {
                if (c == null) continue;
                var typeName = c.GetType().Name;

                bool isGun = MatchesAny(typeName, GunTypeHints);
                bool isWeapon = isGun || MatchesAny(typeName, WeaponTypeHints);

                if (!isWeapon) continue;

                if (isGun)
                {
                    if (TryTrack(c, typeName, RpmFields, "rpm")) gunHits++;
                    TryTrack(c, typeName, IntervalFields, "interval");
                }

                if (TryTrack(c, typeName, CooldownFields, "cooldown"))
                    weaponHits++;
            }

            if (gunHits == 0 && weaponHits == 0)
                Plugin.Log.LogWarning("WeaponOverdrive: no weapon components found on player aircraft");
            else
                Plugin.Log.LogInfo($"WeaponOverdrive: {_tracked.Count} field(s) — {gunHits} gun(s), {weaponHits} cooldown(s)");
        }

        private static bool TryTrack(Component c, string typeName, string[] fieldCandidates, string tag)
        {
            foreach (var name in fieldCandidates)
            {
                var f = AccessTools.Field(c.GetType(), name);
                if (f == null) continue;
                if (f.FieldType != typeof(float) && f.FieldType != typeof(double)) continue;

                float stock = f.FieldType == typeof(float)
                    ? (float)f.GetValue(c)
                    : (float)(double)f.GetValue(c);

                _tracked.Add(new Tracked { Comp = c, Field = f, Stock = stock, Tag = tag });

                string target = tag == "rpm" ? $"{GunTargetRpm}" :
                                tag == "interval" ? "0.0001" : "0";
                Plugin.Log.LogInfo($"WeaponOverdrive: tracking {typeName}.{name} [{tag}] (stock={stock:0.###} → {target})");
                return true;
            }
            return false;
        }

        private static bool MatchesAny(string typeName, string[] hints)
        {
            foreach (var h in hints)
                if (typeName.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        public static void Restore()
        {
            foreach (var t in _tracked)
            {
                if (t.Comp == null) continue;
                try
                {
                    if (t.Field.FieldType == typeof(float))
                        t.Field.SetValue(t.Comp, t.Stock);
                    else if (t.Field.FieldType == typeof(double))
                        t.Field.SetValue(t.Comp, (double)t.Stock);
                }
                catch { }
            }
            _tracked.Clear();
            _trackedActor = null;
        }
    }
}
