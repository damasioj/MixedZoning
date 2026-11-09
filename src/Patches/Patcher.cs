using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MixedZoning.Patches
{
    /// <summary>
    /// Applies every [HarmonyPatch] class in this assembly, all-or-nothing.
    ///
    /// The patches only work as a set: growing buildings in mixed zones without the
    /// Building.CheckZoning patch would get them abandoned right away. So if any
    /// transpiler can't find its target (typically after a game update), everything is
    /// unpatched and mixed zones stay inert until the mod is updated.
    ///
    /// Not yet handled: overlay colours for zones 8..14 and the zoning-panel buttons.
    /// </summary>
    internal static class Patcher
    {
        private static bool patched;
        private static readonly List<string> failures = new List<string>();

        public static bool Active => patched;

        internal static void RecordFailure(string where) => failures.Add(where);

        public static void PatchAll()
        {
            if (patched) return;
            failures.Clear();
            Debug.Log("[MixedZoning] Applying Harmony patches");
            var harmony = new Harmony(Mod.HarmonyId);
            try
            {
                harmony.PatchAll(typeof(Patcher).Assembly);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MixedZoning] Patching threw: {e}");
                failures.Add("exception");
            }

            if (failures.Count > 0)
            {
                Debug.LogError($"[MixedZoning] {failures.Count} patch(es) failed ({string.Join(", ", failures.ToArray())}); " +
                               "removing all patches so the game runs unmodified.");
                harmony.UnpatchAll(Mod.HarmonyId);
                return;
            }
            patched = true;
            Debug.Log("[MixedZoning] Patches applied");
        }

        public static void UnpatchAll()
        {
            if (!patched) return;
            new Harmony(Mod.HarmonyId).UnpatchAll(Mod.HarmonyId);
            patched = false;
        }
    }
}
