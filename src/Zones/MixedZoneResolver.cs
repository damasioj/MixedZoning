using ColossalFramework;
using ColossalFramework.Math;
using MixedZoning.Config;
using UnityEngine;

namespace MixedZoning.Zones
{
    /// <summary>
    /// Replacements for ZoneBlock.GetZone at the three places the game decides what grows
    /// and whether a building still sits on matching zoning. Everything else in the game keeps
    /// seeing the raw stored value (8..14).
    ///
    /// All of this runs on the simulation thread, one ZoneBlock.SimulationStep at a time,
    /// so the origin state below is safe as plain statics.
    /// </summary>
    public static class MixedZoneResolver
    {
        // What the current ZoneBlock.SimulationStep picked for its origin cell.
        private static bool s_originMixed;
        private static ItemClass.Zone s_originRaw;
        private static ItemClass.Zone s_originResolved;

        /// <summary>
        /// Called in place of GetZone(0, row) inside ZoneBlock.SimulationStep. A mixed cell is
        /// turned into one concrete vanilla zone, so the rest of the method (demand check,
        /// m_goodAreaFound[(int)zone], building selection) only ever sees values 0..7.
        /// </summary>
        public static ItemClass.Zone ResolveOrigin(ref ZoneBlock block, int x, int z)
        {
            ItemClass.Zone raw = block.GetZone(x, z);
            if (!MixedZones.IsMixed(raw))
            {
                s_originMixed = false;
                return raw;
            }

            ZoneMix mix = MixConfig.Current.ForZone(raw);
            ItemClass.Zone chosen = mix == null ? ItemClass.Zone.Unzoned : Choose(mix, block.m_position);
            s_originMixed = chosen != ItemClass.Zone.Unzoned;
            s_originRaw = raw;
            s_originResolved = chosen;
            return chosen;
        }

        /// <summary>
        /// Called in place of other.GetZone(x, z) inside ZoneBlock.CheckBlock, which measures how
        /// big a lot can be. For a mixed origin, only cells painted with the same mixed zone
        /// count; for a vanilla origin, mixed cells never count.
        /// </summary>
        public static ItemClass.Zone ForLotMatching(ref ZoneBlock other, int x, int z)
        {
            ItemClass.Zone raw = other.GetZone(x, z);
            if (!s_originMixed)
            {
                return raw;
            }
            return raw == s_originRaw ? s_originResolved : ItemClass.Zone.None;
        }

        /// <summary>
        /// Called in place of block.GetZone(x, z) inside Building.CheckZoning. A mixed cell
        /// counts as matching if its mix allows the building's zone, so buildings grown in a
        /// mixed zone aren't treated as wrongly zoned and abandoned.
        /// </summary>
        public static ItemClass.Zone ForBuildingCheck(ref ZoneBlock block, int x, int z, ItemClass.Zone zone1, ItemClass.Zone zone2)
        {
            ItemClass.Zone raw = block.GetZone(x, z);
            if (!MixedZones.IsMixed(raw))
            {
                return raw;
            }
            ZoneMix mix = MixConfig.Current.ForZone(raw);
            if (mix == null)
            {
                return raw;
            }
            if (mix.Allows(zone1))
            {
                return zone1;
            }
            if (zone2 != ItemClass.Zone.None && mix.Allows(zone2))
            {
                return zone2;
            }
            return raw;
        }

        /// <summary>
        /// Pick one allowed vanilla zone, weighted by its current demand at this location.
        /// When a mix allows both densities of a category, high density is used once that
        /// category's demand reaches the configured threshold.
        /// </summary>
        private static ItemClass.Zone Choose(ZoneMix mix, Vector3 position)
        {
            ZoneManager zm = Singleton<ZoneManager>.instance;
            DistrictManager dm = Singleton<DistrictManager>.instance;
            ref District district = ref dm.m_districts.m_buffer[dm.GetDistrict(position)];
            int threshold = MixConfig.Current.HighDensityDemandThreshold;

            // Up to 6 candidates; fixed arrays avoid allocating every simulation step.
            ItemClass.Zone[] zones = s_zones;
            int[] weights = s_weights;
            int count = 0;

            bool resLow = mix.ResidentialLow, resHigh = mix.ResidentialHigh;
            if (resLow || resHigh)
            {
                int low = zm.m_actualResidentialDemand + district.CalculateResidentialLowDemandOffset();
                int high = zm.m_actualResidentialDemand + district.CalculateResidentialHighDemandOffset();
                if (resLow && resHigh)
                {
                    if (PreferHigh(high, threshold, ItemClass.Zone.ResidentialLow, ItemClass.Zone.ResidentialHigh)) resLow = false; else resHigh = false;
                }
                if (resLow) { zones[count] = ItemClass.Zone.ResidentialLow; weights[count++] = low; }
                if (resHigh) { zones[count] = ItemClass.Zone.ResidentialHigh; weights[count++] = high; }
            }

            bool comLow = mix.CommercialLow, comHigh = mix.CommercialHigh;
            if (comLow || comHigh)
            {
                int low = zm.m_actualCommercialDemand + district.CalculateCommercialLowDemandOffset();
                int high = zm.m_actualCommercialDemand + district.CalculateCommercialHighDemandOffset();
                if (comLow && comHigh)
                {
                    if (PreferHigh(high, threshold, ItemClass.Zone.CommercialLow, ItemClass.Zone.CommercialHigh)) comLow = false; else comHigh = false;
                }
                if (comLow) { zones[count] = ItemClass.Zone.CommercialLow; weights[count++] = low; }
                if (comHigh) { zones[count] = ItemClass.Zone.CommercialHigh; weights[count++] = high; }
            }

            if (mix.Industrial)
            {
                zones[count] = ItemClass.Zone.Industrial;
                weights[count++] = zm.m_actualWorkplaceDemand + district.CalculateIndustrialDemandOffset();
            }
            if (mix.Office)
            {
                zones[count] = ItemClass.Zone.Office;
                weights[count++] = zm.m_actualWorkplaceDemand + district.CalculateOfficeDemandOffset();
            }

            // Never grow a zone type the player hasn't unlocked yet.
            int kept = 0;
            for (int i = 0; i < count; i++)
            {
                if (!Unlocked(zones[i])) continue;
                zones[kept] = zones[i];
                weights[kept++] = weights[i];
            }
            count = kept;

            if (count == 0)
            {
                return ItemClass.Zone.Unzoned;
            }

            // Randomizer is a struct: use it by reference so its state advances like the game's own rolls.
            ref Randomizer rand = ref Singleton<SimulationManager>.instance.m_randomizer;
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                if (weights[i] > 0) total += weights[i];
            }
            ItemClass.Zone result;
            if (total == 0)
            {
                // No demand anywhere: pick any; the game's own demand roll will then decline to build.
                result = zones[rand.Int32((uint)count)];
            }
            else
            {
                int roll = rand.Int32((uint)total);
                result = zones[count - 1];
                for (int i = 0; i < count; i++)
                {
                    if (weights[i] <= 0) continue;
                    if (roll < weights[i]) { result = zones[i]; break; }
                    roll -= weights[i];
                }
            }
            return result;
        }

        private static bool Unlocked(ItemClass.Zone zone) =>
            !Singleton<UnlockManager>.exists || Singleton<UnlockManager>.instance.Unlocked(zone);

        /// <summary>High density when demand reaches the threshold, unless only one of the two is unlocked.</summary>
        private static bool PreferHigh(int highDemand, int threshold, ItemClass.Zone low, ItemClass.Zone high)
        {
            bool lowOk = Unlocked(low), highOk = Unlocked(high);
            if (lowOk != highOk) return highOk;
            return highDemand >= threshold;
        }

        private static readonly ItemClass.Zone[] s_zones = new ItemClass.Zone[6];
        private static readonly int[] s_weights = new int[6];
    }
}
