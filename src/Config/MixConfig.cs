using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;

namespace MixedZoning.Config
{
    /// <summary>
    /// One custom zone and the vanilla building zones allowed to grow in it.
    /// </summary>
    public class ZoneMix
    {
        [XmlAttribute] public int Slot;          // 0..6 -> MixedZones.First + Slot
        public bool ResidentialLow;
        public bool ResidentialHigh;
        public bool CommercialLow;
        public bool CommercialHigh;
        public bool Industrial;
        public bool Office;

        public bool Allows(ItemClass.Zone zone)
        {
            switch (zone)
            {
                case ItemClass.Zone.ResidentialLow: return ResidentialLow;
                case ItemClass.Zone.ResidentialHigh: return ResidentialHigh;
                case ItemClass.Zone.CommercialLow: return CommercialLow;
                case ItemClass.Zone.CommercialHigh: return CommercialHigh;
                case ItemClass.Zone.Industrial: return Industrial;
                case ItemClass.Zone.Office: return Office;
                default: return false;
            }
        }

        private static readonly ItemClass.Zone[] VanillaBuildZones =
        {
            ItemClass.Zone.ResidentialLow, ItemClass.Zone.ResidentialHigh,
            ItemClass.Zone.CommercialLow, ItemClass.Zone.CommercialHigh,
            ItemClass.Zone.Industrial, ItemClass.Zone.Office,
        };

        [XmlIgnore] private ItemClass.Zone[] allowedCache;

        /// <summary>Allowed vanilla zones in a fixed order. Cached: the terrain overlay calls this per pixel.</summary>
        public ItemClass.Zone[] AllowedZones()
        {
            if (allowedCache == null)
            {
                var list = new List<ItemClass.Zone>(6);
                foreach (var z in VanillaBuildZones)
                {
                    if (Allows(z)) list.Add(z);
                }
                allowedCache = list.ToArray();
            }
            return allowedCache;
        }

        internal void InvalidateCache() => allowedCache = null;
    }

    [XmlRoot("MixedZoning")]
    public class MixConfig
    {
        public List<ZoneMix> Mixes = new List<ZoneMix>();

        /// <summary>
        /// When a mix allows both densities of the same category, the game has to pick one.
        /// Off (default): low or high at random, 50/50. On: high density once that category's
        /// demand reaches HighDensityDemandThreshold, low density below it.
        /// </summary>
        public bool UseHighDensityThreshold = false;

        /// <summary>Low-vs-high density demand cutoff (0..100); only used when UseHighDensityThreshold is on.</summary>
        public int HighDensityDemandThreshold = 50;

        public static MixConfig Current { get; private set; } = Defaults();

        public static string FilePath =>
            Path.Combine(ColossalFramework.IO.DataLocation.localApplicationData, "MixedZoningConfig.xml");

        public ZoneMix ForZone(ItemClass.Zone zone) => ForSlot((int)zone - MixedZones.First);

        public ZoneMix ForSlot(int slot)
        {
            if (slot < 0 || slot >= MixedZones.Count) return null;
            foreach (var mix in Mixes)
            {
                if (mix.Slot == slot) return mix;
            }
            return null;
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    Current = Defaults();
                    Save();
                    return;
                }
                using (var stream = File.OpenRead(FilePath))
                {
                    Current = (MixConfig)new XmlSerializer(typeof(MixConfig)).Deserialize(stream) ?? Defaults();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MixedZoning] Could not read {FilePath}, using defaults: {e}");
                Current = Defaults();
            }
        }

        public static void Save()
        {
            foreach (var mix in Current.Mixes) mix.InvalidateCache();
            try
            {
                using (var stream = File.Create(FilePath))
                {
                    new XmlSerializer(typeof(MixConfig)).Serialize(stream, Current);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MixedZoning] Could not write {FilePath}: {e}");
            }
        }

        private static MixConfig Defaults()
        {
            return new MixConfig
            {
                Mixes = new List<ZoneMix>
                {
                    new ZoneMix { Slot = 0, ResidentialLow = true,  CommercialLow = true },
                    new ZoneMix { Slot = 1, ResidentialHigh = true, CommercialHigh = true },
                    new ZoneMix { Slot = 2, CommercialLow = true,   Office = true },
                    new ZoneMix { Slot = 3, ResidentialHigh = true, Office = true },
                    new ZoneMix { Slot = 4, Industrial = true,      Office = true },
                    new ZoneMix { Slot = 5, CommercialLow = true,   CommercialHigh = true, Office = true },
                    new ZoneMix { Slot = 6, ResidentialLow = true,  ResidentialHigh = true, CommercialLow = true, CommercialHigh = true, Office = true },
                },
            };
        }
    }

    /// <summary>
    /// Zone cells store their zone in 4 bits, so 16 values are possible. Vanilla uses
    /// 0..7 and 15 (None); this mod claims the gap 8..14 (verified against the game's
    /// ItemClass.Zone enum, Nov 2026).
    ///
    /// Save-safety rules (from reading the game's ZoneManager/ZoneBlock code):
    ///  - ZoneManager.Data.Serialize writes block cells as raw 64-bit masks, so values
    ///    8..14 survive save/load without any patch.
    ///  - ZoneManager.m_goodAreaFound and m_zoneNotUsed are fixed 8-entry arrays.
    ///    ZoneBlock.SimulationStep indexes m_goodAreaFound by the cell's zone, so a mixed
    ///    zone must be resolved to a vanilla zone BEFORE that point, never passed through.
    ///  - Never resize those game arrays; the serializer loops a hard-coded 8 entries.
    /// </summary>
    public static class MixedZones
    {
        public const int First = 8;
        public const int Count = 7;

        public static string DisplayName(int slot) => $"Mixed Zone {slot + 1}";

        public static bool IsMixed(ItemClass.Zone zone) =>
            (int)zone >= First && (int)zone < First + Count;
    }
}
