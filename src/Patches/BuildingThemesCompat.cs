using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MixedZoning.Zones;
using UnityEngine;

namespace MixedZoning.Patches
{
    /// <summary>
    /// Building Themes replaces ZoneBlock.SimulationStep with its own copy
    /// (BuildingThemes.Detour.ZoneBlockDetour.SimulationStep) by redirecting the game method
    /// at level load, which bypasses our transpiler on the game method. Its copy keeps the
    /// game's single GetZone(0, row) origin lookup, so the same swap is applied to the copy.
    ///
    /// Its CheckBlock is a reverse redirect to the game's method, which is already patched,
    /// and Building.CheckZoning isn't touched by it.
    ///
    /// Optional: if Building Themes isn't loaded, or its copy doesn't look as expected,
    /// nothing is patched and the rest of the mod is unaffected.
    /// </summary>
    internal static class BuildingThemesCompat
    {
        private const string DetourType = "BuildingThemes.Detour.ZoneBlockDetour";
        private static bool attempted;

        internal static void Reset() => attempted = false;

        /// <summary>Patch Building Themes' copy if it's loaded. Safe to call repeatedly.</summary>
        internal static void TryPatch(Harmony harmony)
        {
            if (attempted) return;
            Type type = FindType();
            if (type == null) return;   // not loaded (yet); try again on the next level load
            attempted = true;

            MethodInfo method = AccessTools.Method(type, "SimulationStep", new[] { typeof(ZoneBlock).MakeByRefType(), typeof(ushort) });
            if (method == null)
            {
                Debug.LogWarning("[MixedZoning] Building Themes found but its SimulationStep wasn't; mixed zones won't grow in themed areas.");
                return;
            }
            int calls = PatchProcessor.ReadMethodBody(method).Count(i => Equals(i.Value, GetZoneCallSwap.GetZone));
            if (calls != 1)
            {
                Debug.LogWarning($"[MixedZoning] Building Themes' SimulationStep has {calls} GetZone calls (expected 1); not patched.");
                return;
            }
            try
            {
                harmony.Patch(method, transpiler: new HarmonyMethod(typeof(BuildingThemesCompat), nameof(Transpiler)));
                Debug.Log("[MixedZoning] Patched Building Themes' zone growth for mixed zones");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MixedZoning] Could not patch Building Themes: {e}");
            }
        }

        internal static Type FindType()
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t;
                try { t = asm.GetType(DetourType, false); }
                catch { continue; }
                if (t != null) return t;
            }
            return null;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            GetZoneCallSwap.Apply(instructions, "BuildingThemes ZoneBlockDetour.SimulationStep",
                AccessTools.Method(typeof(MixedZoneResolver), nameof(MixedZoneResolver.ResolveOrigin)), 1);
    }
}
