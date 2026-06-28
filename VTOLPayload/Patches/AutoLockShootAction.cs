using System.Reflection;
using HarmonyLib;
using UnityEngine;
using VTOLTrainer;

namespace VTOLPayload.Patches
{
    /// One-shot: find the player's currently-equipped weapon, pick the nearest hostile
    /// from TargetManager.enemyUnits, jam that hostile into whatever "locked target" field
    /// the equip exposes, and call Fire(). Designed as a panic button — single missile per
    /// press, no hold-to-spam. Logs verbosely on first use so we can adapt to whatever
    /// field/method names VTOL VR is using this build.
    internal static class AutoLockShootAction
    {
        // Field names we'll try to drop the target into (in order). Cover several VTOL VR revisions.
        private static readonly string[] TargetFieldCandidates =
        {
            "lockedActor", "lockedTarget", "currentTarget", "target",
            "radarTarget", "irTarget", "designatedTarget"
        };

        // Method names we'll try to invoke for "fire" (in order).
        private static readonly string[] FireMethodCandidates =
        {
            "FireWeapon", "Fire", "LaunchMissile", "Launch", "ManualFire"
        };

        public static void Tick()
        {
            if (!PluginState.RequestAutoLockShoot) return;
            PluginState.RequestAutoLockShoot = false;

            var actor = FlightSceneManager.instance ? FlightSceneManager.instance.playerActor : null;
            if (actor == null) { Plugin.Log.LogWarning("AutoLockShoot: no player actor"); return; }

            // 1. Pick nearest hostile.
            var target = FindNearestHostile(actor);
            if (target == null) { Plugin.Log.LogWarning("AutoLockShoot: no hostile in range"); return; }
            Plugin.Log.LogInfo($"AutoLockShoot: target = {target.actorName} @ {(target.position - actor.position).magnitude:0}m");

            // 2. Find weapon manager and current equip.
            var wm = actor.GetComponentInChildren<WeaponManager>();
            if (wm == null) { Plugin.Log.LogWarning("AutoLockShoot: no WeaponManager on player"); return; }

            var equip = GetCurrentEquip(wm);
            if (equip == null) { Plugin.Log.LogWarning("AutoLockShoot: no current equip"); return; }
            Plugin.Log.LogInfo($"AutoLockShoot: equip = {equip.GetType().Name}");

            // 3. Slam the target into the equip's locked-target field.
            bool slammed = TrySetTarget(equip, target);
            if (!slammed) Plugin.Log.LogWarning("AutoLockShoot: couldn't find a target field on the equip (firing anyway)");

            // 4. Fire.
            bool fired = TryFire(equip);
            if (!fired) Plugin.Log.LogWarning("AutoLockShoot: couldn't find a fire method on the equip");
        }

        private static Actor FindNearestHostile(Actor self)
        {
            if (TargetManager.instance == null) return null;
            Actor best = null;
            float bestSq = float.MaxValue;
            foreach (var e in TargetManager.instance.enemyUnits)
            {
                if (e == null) continue;
                var h = e.GetComponentInChildren<Health>();
                if (h != null && h.isDead) continue;
                float sq = (e.position - self.position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = e; }
            }
            return best;
        }

        private static Component GetCurrentEquip(WeaponManager wm)
        {
            // Try a few likely accessors via Traverse — keeps us version-tolerant.
            var tr = Traverse.Create(wm);

            // Property/field forms commonly seen in VTOL VR builds:
            foreach (var name in new[] { "CurrentEquip", "currentEquip", "GetCurrentEquip" })
            {
                var p = tr.Property(name);
                if (p.PropertyExists()) { var v = p.GetValue() as Component; if (v != null) return v; }
                var f = tr.Field(name);
                if (f.FieldExists()) { var v = f.GetValue() as Component; if (v != null) return v; }
                var m = tr.Method(name);
                if (m.MethodExists()) { var v = m.GetValue() as Component; if (v != null) return v; }
            }
            return null;
        }

        private static bool TrySetTarget(Component equip, Actor target)
        {
            var tr = Traverse.Create(equip);

            // First, the obvious field names.
            foreach (var name in TargetFieldCandidates)
            {
                var f = tr.Field(name);
                if (!f.FieldExists()) continue;
                try { f.SetValue(target); Plugin.Log.LogInfo($"  · set {name} = target"); return true; }
                catch { /* type mismatch — try next */ }
            }

            // Then any setter method like SetTarget / SetLock.
            foreach (var name in new[] { "SetTarget", "SetLock", "SetLockedTarget", "LockTarget" })
            {
                var m = AccessTools.Method(equip.GetType(), name, new[] { typeof(Actor) });
                if (m == null) continue;
                try { m.Invoke(equip, new object[] { target }); Plugin.Log.LogInfo($"  · called {name}(target)"); return true; }
                catch { /* try next */ }
            }
            return false;
        }

        private static bool TryFire(Component equip)
        {
            foreach (var name in FireMethodCandidates)
            {
                // Prefer zero-arg overloads — anything more exotic and we'd be guessing parameter values.
                var m = AccessTools.Method(equip.GetType(), name, System.Type.EmptyTypes);
                if (m == null) continue;
                try { m.Invoke(equip, null); Plugin.Log.LogInfo($"  · called {name}()"); return true; }
                catch (System.Exception ex)
                {
                    Plugin.Log.LogWarning($"  · {name}() threw: {ex.InnerException?.Message ?? ex.Message}");
                }
            }
            return false;
        }
    }
}
