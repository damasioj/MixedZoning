using System.Collections.Generic;
using System.Reflection.Emit;
using ColossalFramework;
using HarmonyLib;
using MixedZoning.Config;
using MixedZoning.Zones;

namespace MixedZoning.Patches
{
    /// <summary>Growth: resolve a mixed origin cell to one vanilla zone.</summary>
    [HarmonyPatch(typeof(ZoneBlock), nameof(ZoneBlock.SimulationStep))]
    internal static class ZoneBlock_SimulationStep
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            GetZoneCallSwap.Apply(instructions, "ZoneBlock.SimulationStep",
                AccessTools.Method(typeof(MixedZoneResolver), nameof(MixedZoneResolver.ResolveOrigin)), 1);
    }

    /// <summary>Growth: lot sizing only joins cells of the same mixed zone.</summary>
    [HarmonyPatch(typeof(ZoneBlock), "CheckBlock")]
    internal static class ZoneBlock_CheckBlock
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            GetZoneCallSwap.Apply(instructions, "ZoneBlock.CheckBlock",
                AccessTools.Method(typeof(MixedZoneResolver), nameof(MixedZoneResolver.ForLotMatching)), 1);
    }

    /// <summary>
    /// Keeps grown buildings valid: the private overload
    /// CheckZoning(Zone zone1, Zone zone2, ref uint, ref bool, ref ZoneBlock) is where each
    /// cell under a building is compared to the building's zone. zone1/zone2 are pushed as
    /// extra helper arguments (arg 0 is the Building&amp; this).
    /// </summary>
    [HarmonyPatch(typeof(Building), "CheckZoning",
        new[] { typeof(ItemClass.Zone), typeof(ItemClass.Zone), typeof(uint), typeof(bool), typeof(ZoneBlock) },
        new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Ref, ArgumentType.Ref })]
    internal static class Building_CheckZoning
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            GetZoneCallSwap.Apply(instructions, "Building.CheckZoning",
                AccessTools.Method(typeof(MixedZoneResolver), nameof(MixedZoneResolver.ForBuildingCheck)), 1,
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldarg_2));
    }

    /// <summary>
    /// ZoneTool.UsedZone indexes ZoneManager.m_zoneNotUsed (8 entries) by the painted zone,
    /// which would throw for a mixed zone. For mixed zones, only clear the general
    /// "you haven't zoned yet" hint and skip the per-zone one.
    /// </summary>
    [HarmonyPatch(typeof(ZoneTool), "UsedZone")]
    internal static class ZoneTool_UsedZone
    {
        private static bool Prefix(ItemClass.Zone zone)
        {
            if (!MixedZones.IsMixed(zone))
            {
                return true;
            }
            Singleton<ZoneManager>.instance.m_zonesNotUsed?.Disable();
            return false;
        }
    }
}
