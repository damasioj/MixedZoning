using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MixedZoning.Patches
{
    /// <summary>
    /// Shared transpiler helper: replaces calls to ZoneBlock.GetZone(int, int) with a static
    /// helper whose first three parameters are (ref ZoneBlock, int, int), so the stack layout
    /// is unchanged. Optional extra instructions are inserted right before the call to push
    /// additional arguments.
    ///
    /// If the method doesn't contain exactly the expected number of GetZone calls (e.g. after
    /// a game update), the original code is returned untouched and an error is logged:
    /// the mixed zones then simply don't grow, instead of the game crashing.
    /// </summary>
    internal static class GetZoneCallSwap
    {
        public static readonly MethodInfo GetZone =
            AccessTools.Method(typeof(ZoneBlock), nameof(ZoneBlock.GetZone), new[] { typeof(int), typeof(int) });

        public static IEnumerable<CodeInstruction> Apply(
            IEnumerable<CodeInstruction> instructions,
            string where,
            MethodInfo replacement,
            int expectedCalls,
            params CodeInstruction[] extraArgs)
        {
            List<CodeInstruction> codes = instructions.ToList();
            int found = codes.Count(c => c.Calls(GetZone));
            if (found != expectedCalls)
            {
                Debug.LogError($"[MixedZoning] {where}: expected {expectedCalls} GetZone call(s), found {found}. " +
                               "The game has probably changed; this patch is disabled.");
                Patcher.RecordFailure(where);
                return codes;
            }

            var result = new List<CodeInstruction>(codes.Count + extraArgs.Length * expectedCalls);
            foreach (CodeInstruction code in codes)
            {
                if (code.Calls(GetZone))
                {
                    // Keep labels/exception blocks on the first instruction we emit.
                    CodeInstruction call = new CodeInstruction(System.Reflection.Emit.OpCodes.Call, replacement);
                    if (extraArgs.Length > 0)
                    {
                        CodeInstruction first = extraArgs[0].Clone();
                        first.labels.AddRange(code.labels);
                        first.blocks.AddRange(code.blocks);
                        result.Add(first);
                        for (int i = 1; i < extraArgs.Length; i++) result.Add(extraArgs[i].Clone());
                    }
                    else
                    {
                        call.labels.AddRange(code.labels);
                        call.blocks.AddRange(code.blocks);
                    }
                    result.Add(call);
                }
                else
                {
                    result.Add(code);
                }
            }
            return result;
        }
    }
}
