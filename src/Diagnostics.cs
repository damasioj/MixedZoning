using System;
using System.Linq;
using System.Reflection;
using System.Text;
using ColossalFramework;
using HarmonyLib;
using UnityEngine;

namespace MixedZoning
{
    /// <summary>
    /// Troubleshooting log (look for "[MixedZoning][diag]" in Player.log / output_log.txt): at
    /// level load, which mods patch the zone-growth methods, so conflicts show up in user logs.
    /// </summary>
    internal static class Diagnostics
    {
        public static void LogPatchState()
        {
            try
            {
                var sb = new StringBuilder("[MixedZoning][diag] Patch state at level load:");
                Report(sb, AccessTools.Method(typeof(ZoneBlock), "SimulationStep"));
                Report(sb, AccessTools.Method(typeof(ZoneBlock), "CheckBlock"));
                Report(sb, AccessTools.Method(typeof(Building), "CheckZoning",
                    new[] { typeof(ItemClass.Zone), typeof(ItemClass.Zone), typeof(uint).MakeByRefType(), typeof(bool).MakeByRefType(), typeof(ZoneBlock).MakeByRefType() }));
                Report(sb, AccessTools.Method(typeof(Building), "CheckZoning", new[] { typeof(ItemClass.Zone), typeof(ItemClass.Zone), typeof(bool) }));
                Report(sb, AccessTools.Method(typeof(ZoneManager), "SimulationStepImpl"));
                Type bt = Patches.BuildingThemesCompat.FindType();
                if (bt != null) Report(sb, AccessTools.Method(bt, "SimulationStep"));
                ZoneManager zm = Singleton<ZoneManager>.instance;
                sb.Append($"\n  zone grid: {zm.m_zoneGrid?.Length} cells, blocks: {zm.m_blocks?.m_buffer?.Length}, patcher active: {Patches.Patcher.Active}");
                sb.Append("\n  mods with Harmony patches: " + string.Join(", ", Harmony.GetAllPatchedMethods()
                    .Select(Harmony.GetPatchInfo).Where(i => i != null).SelectMany(i => i.Owners).Distinct().ToArray()));
                Debug.Log(sb.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError($"[MixedZoning][diag] patch state failed: {e}");
            }
        }

        private static void Report(StringBuilder sb, MethodBase m)
        {
            if (m == null) { sb.Append("\n  (method not found)"); return; }
            HarmonyLib.Patches info = Harmony.GetPatchInfo(m);
            string Owners(System.Collections.ObjectModel.ReadOnlyCollection<Patch> ps) =>
                ps == null || ps.Count == 0 ? "-" : string.Join(", ", ps.Select(p => p.owner + "#" + p.index).ToArray());
            sb.Append($"\n  {m.DeclaringType?.Name}.{m.Name}({m.GetParameters().Length} args): " +
                      (info == null ? "not patched" :
                      $"prefixes [{Owners(info.Prefixes)}] transpilers [{Owners(info.Transpilers)}] postfixes [{Owners(info.Postfixes)}]"));
        }
    }
}
