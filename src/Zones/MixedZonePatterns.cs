using ColossalFramework;
using UnityEngine;

namespace MixedZoning.Zones
{
    /// <summary>
    /// Fixed look of each mixed zone: a checkerboard of two colours (or one solid colour), one per
    /// zone cell. It's deliberately independent of what a zone is configured to grow, so players
    /// can set each one up however they like; the colours only identify the zone.
    ///
    /// The terrain zone shader only has the 8 vanilla colour slots, so the overlay draws each
    /// mixed cell with one of its zone's two colours: vanilla zone colours, plus a light purple
    /// in the borrowed "Distant" slot. Mixed cells are also drawn turned
    /// 45° (see TerrainManager_GetZoneCell) so they don't read as vanilla zones.
    /// </summary>
    public static class MixedZonePatterns
    {
        /// <summary>Light purple, a colour no vanilla zone uses.</summary>
        public static readonly Color Purple = new Color(0.76f, 0.60f, 0.93f, 1f);

        /// <summary>
        /// Colour slot the purple is drawn with on the terrain. Slot 1 ("Distant") is never stored
        /// in zone cells (ZoneBlock.SetZone rejects it), so the mod borrows it while a city is
        /// loaded and restores it afterwards.
        /// </summary>
        public const ItemClass.Zone PurpleSlot = ItemClass.Zone.Distant;

        /// <summary>The two checker colours (as colour slots) for each mixed zone.</summary>
        private static readonly ItemClass.Zone[,] Colors =
        {
            { PurpleSlot,                     PurpleSlot },                     // Mixed Zone 1: light purple (solid)
            { ItemClass.Zone.ResidentialLow,  ItemClass.Zone.ResidentialHigh }, // Mixed Zone 2: light green + dark green
            { ItemClass.Zone.CommercialLow,   ItemClass.Zone.CommercialHigh },  // Mixed Zone 3: light blue + dark blue
            { ItemClass.Zone.ResidentialLow,  ItemClass.Zone.CommercialLow },   // Mixed Zone 4: light green + light blue
            { ItemClass.Zone.ResidentialHigh, ItemClass.Zone.Office },          // Mixed Zone 5: dark green + office blue
            { ItemClass.Zone.Office,          ItemClass.Zone.Industrial },      // Mixed Zone 6: office blue + yellow
            { PurpleSlot,                     ItemClass.Zone.Industrial },      // Mixed Zone 7: light purple + yellow
        };

        public static ItemClass.Zone First(int slot) => Colors[slot, 0];
        public static ItemClass.Zone Second(int slot) => Colors[slot, 1];

        /// <summary>Whether grid position (a, b) takes the first colour (checkerboard).</summary>
        public static bool On(int a, int b) => ((a ^ b) & 1) == 0;

        /// <summary>Colour slot to draw at zone-cell position (a, b) on the terrain overlay.</summary>
        public static ItemClass.Zone At(int slot, int a, int b) => On(a, b) ? First(slot) : Second(slot);

        /// <summary>Colour of a slot, taken from the game so icons match the overlay.</summary>
        public static Color ZoneColor(ItemClass.Zone zone)
        {
            if (zone == PurpleSlot) return Purple;
            Color[] colors = Singleton<ZoneManager>.exists ? Singleton<ZoneManager>.instance.m_properties?.m_zoneColors : null;
            int i = (int)zone;
            if (colors != null && i < colors.Length) return colors[i];
            return Color.gray;
        }
    }
}
