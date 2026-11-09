using System.Reflection;
using System.Text;
using ColossalFramework.UI;
using HarmonyLib;
using MixedZoning.Config;
using MixedZoning.UI;
using MixedZoning.Zones;
using UnityEngine;

namespace MixedZoning.Patches
{
    /// <summary>Marks a zoning-panel button as one of ours (stored in objectUserData).</summary>
    internal sealed class MixedZoneButtonTag
    {
        public readonly int Slot;
        public MixedZoneButtonTag(int slot) { Slot = slot; }
        public ItemClass.Zone Zone => (ItemClass.Zone)(MixedZones.First + Slot);
    }

    /// <summary>
    /// Adds the mixed-zone buttons after the game fills the zoning panel. They are kept out of
    /// ZoningPanel.kZones on purpose: the game feeds kZones entries into milestone and unlock
    /// lookups that only have room for the 8 vanilla zones.
    /// </summary>
    [HarmonyPatch(typeof(ZoningPanel), nameof(ZoningPanel.RefreshPanel))]
    internal static class ZoningPanel_RefreshPanel
    {
        private static readonly MethodInfo SpawnEntry = AccessTools.Method(typeof(GeneratedScrollPanel), "SpawnEntry", new[]
        {
            typeof(string), typeof(string), typeof(string), typeof(UITextureAtlas),
            typeof(UIComponent), typeof(bool), typeof(UITextureAtlas), typeof(string),
        });

        private static void Postfix(ZoningPanel __instance)
        {
            try
            {
                UITextureAtlas atlas = MixedZoneIcons.Atlas;
                for (int slot = 0; slot < MixedZones.Count; slot++)
                {
                    string sprite = MixedZoneIcons.SpriteName(slot);
                    var button = (UIButton)SpawnEntry.Invoke(__instance, new object[]
                    {
                        sprite, Tooltip(slot), sprite, atlas, null, AnyUnlocked(slot), null, string.Empty,
                    });
                    button.objectUserData = new MixedZoneButtonTag(slot);
                    // Buttons are reused between refreshes: unsubscribe first so the handler is attached once.
                    button.eventMouseUp -= OnMouseUp;
                    button.eventMouseUp += OnMouseUp;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MixedZoning] Could not add zoning buttons: {e}");
            }
        }

        /// <summary>
        /// Refresh our buttons' tooltips and enabled state after a config change, in place, without
        /// rebuilding the panel (which would reset the player's selection).
        /// </summary>
        public static void UpdateButtons()
        {
            ZoningPanel panel = Object.FindObjectOfType<ZoningPanel>();
            if (panel == null) return;
            foreach (UIComponent child in panel.childComponents)
            {
                if (child is UIButton button && button.objectUserData is MixedZoneButtonTag tag)
                {
                    button.tooltip = Tooltip(tag.Slot);
                    button.isEnabled = AnyUnlocked(tag.Slot);
                    button.RefreshTooltip();
                }
            }
        }

        private static readonly MethodInfo SelectByIndex = AccessTools.Method(typeof(GeneratedScrollPanel), "SelectByIndex", new[] { typeof(int) });
        private static readonly FieldInfo LastSelectedIndex = AccessTools.Field(typeof(GeneratedScrollPanel), "m_LastSelectedIndex");

        /// <summary>
        /// Right-click on a mixed-zone button selects that zone (same as a left-click) and opens
        /// its editor window.
        /// </summary>
        private static void OnMouseUp(UIComponent component, UIMouseEventParameter p)
        {
            if ((p.buttons & UIMouseButton.Right) == 0 || !(component.objectUserData is MixedZoneButtonTag tag)) return;
            ZoningPanel panel = Object.FindObjectOfType<ZoningPanel>();
            if (panel != null)
            {
                // Mirror GeneratedScrollPanel.OnClick: remember + highlight the button, then select the zone.
                int index = component.zOrder;
                LastSelectedIndex.SetValue(panel, index);
                ZoningPanel_OnButtonClicked.SelectZone(panel, tag);
                SelectByIndex.Invoke(panel, new object[] { index });
            }
            MixEditorWindow.Open(tag.Slot);
            p.Use();
        }

        private static bool AnyUnlocked(int slot)
        {
            ZoneMix mix = MixConfig.Current.ForSlot(slot);
            if (mix == null) return false;
            foreach (var z in mix.AllowedZones())
            {
                if (ToolsModifierControl.IsUnlocked(z)) return true;
            }
            return false;
        }

        private static string Tooltip(int slot)
        {
            var sb = new StringBuilder(MixedZones.DisplayName(slot));
            sb.Append("\nGrows: ");
            ItemClass.Zone[] zones = MixConfig.Current.ForSlot(slot)?.AllowedZones() ?? new ItemClass.Zone[0];
            if (zones.Length == 0) sb.Append("nothing yet");
            for (int i = 0; i < zones.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Describe(zones[i]));
            }
            sb.Append("\nRight-click to edit");
            return sb.ToString();
        }

        private static string Describe(ItemClass.Zone zone)
        {
            switch (zone)
            {
                case ItemClass.Zone.ResidentialLow: return "low-density residential";
                case ItemClass.Zone.ResidentialHigh: return "high-density residential";
                case ItemClass.Zone.CommercialLow: return "low-density commercial";
                case ItemClass.Zone.CommercialHigh: return "high-density commercial";
                case ItemClass.Zone.Industrial: return "industrial";
                case ItemClass.Zone.Office: return "office";
                default: return zone.ToString();
            }
        }
    }

    /// <summary>
    /// A left-click on any zoning-panel button (vanilla or mixed) closes the editor window.
    /// Right-clicks are left alone so right-clicking a mixed zone can open it.
    /// OnClick is declared on GeneratedScrollPanel and shared by every build panel, so this
    /// only acts for the zoning panel.
    /// </summary>
    [HarmonyPatch(typeof(GeneratedScrollPanel), "OnClick")]
    internal static class GeneratedScrollPanel_OnClick
    {
        private static void Prefix(GeneratedScrollPanel __instance, UIMouseEventParameter p)
        {
            if (__instance is ZoningPanel && p != null && (p.buttons & UIMouseButton.Left) != 0)
            {
                MixEditorWindow.Close();
            }
        }
    }

    /// <summary>
    /// ZoningPanel.OnButtonClicked looks the zone up as kZones[button.zOrder], which is out of
    /// range for our buttons. Handle ours here instead.
    /// </summary>
    [HarmonyPatch(typeof(ZoningPanel), "OnButtonClicked")]
    internal static class ZoningPanel_OnButtonClicked
    {
        private static readonly MethodInfo ShowZoningOptionPanel =
            AccessTools.Method(typeof(GeneratedScrollPanel), "ShowZoningOptionPanel");

        private static bool Prefix(ZoningPanel __instance, UIComponent comp)
        {
            if (!(comp.objectUserData is MixedZoneButtonTag tag)) return true;
            SelectZone(__instance, tag);
            return false;
        }

        /// <summary>Switch to the zoning tool with this mixed zone, as the game does for its own zones.</summary>
        internal static void SelectZone(ZoningPanel panel, MixedZoneButtonTag tag)
        {
            ZoneTool tool = ToolsModifierControl.SetTool<ZoneTool>();
            if (tool != null)
            {
                ShowZoningOptionPanel.Invoke(panel, null);
                tool.m_zone = tag.Zone;
            }
        }
    }

    /// <summary>
    /// ZoningPanel.OnTooltipHover casts objectUserData to int and reads milestone data for it;
    /// our buttons use a plain tooltip, so skip it for them.
    /// </summary>
    [HarmonyPatch(typeof(ZoningPanel), nameof(ZoningPanel.OnTooltipHover), new[] { typeof(UIComponent), typeof(UIMouseEventParameter) })]
    internal static class ZoningPanel_OnTooltipHover
    {
        private static bool Prefix(UIMouseEventParameter p) =>
            !(p?.source is UIButton b && b.objectUserData is MixedZoneButtonTag);
    }

    /// <summary>
    /// Draws mixed cells in the terrain zone overlay with each zone's fixed colour pattern
    /// (MixedZonePatterns), independent of configuration. The zone shader only has 8 colour
    /// slots, so zones 8..14 can't have colours of their own. GetZoneCell is only called by
    /// TerrainPatch.Refresh (the overlay texture builder), so this doesn't affect simulation.
    /// The cell byte is zone + 16 when occupied; keep that bit.
    ///
    /// The pattern is laid out on the zone cells themselves (ZoneCellLocator), so it turns with
    /// the road instead of following the map grid.
    /// </summary>
    [HarmonyPatch(typeof(TerrainManager), nameof(TerrainManager.GetZoneCell))]
    internal static class TerrainManager_GetZoneCell
    {
        private static void Postfix(int x, int z, ref TerrainManager.ZoneCell __result)
        {
            int zone = __result.m_zone & 15;
            if (!MixedZones.IsMixed((ItemClass.Zone)zone)) return;
            ZoneCellLocator.CellCoords(x, z, __result, out int a, out int b);
            ItemClass.Zone shown = MixedZonePatterns.At(zone - MixedZones.First, a, b);
            __result.m_zone = (byte)((__result.m_zone & 16) | (int)shown);
            // Draw mixed cells turned 45° (diamonds) so they stand apart from vanilla squares.
            // The angle byte has 256 steps per 90° (squares repeat every 90°), so +128 = 45°.
            // The offset to the cell centre is in world space, so the diamond stays centred.
            __result.m_angle = (byte)(__result.m_angle + 128);
        }
    }

    /// <summary>
    /// Esc closes the editor window. The game handles Esc as a priority chain (tutorial, info
    /// panels, tools, then the pause menu), closing one thing per press; the window goes first,
    /// so while it's open Esc closes only the window. Covers keyboard and Steam controller.
    /// </summary>
    [HarmonyPatch(typeof(GameKeyShortcuts), "Escape")]
    internal static class GameKeyShortcuts_Escape
    {
        private static bool Prefix() => !MixEditorWindow.CloseIfOpen();
    }

    [HarmonyPatch(typeof(GameKeyShortcuts), "SteamEscape")]
    internal static class GameKeyShortcuts_SteamEscape
    {
        private static bool Prefix() => !MixEditorWindow.CloseIfOpen();
    }
}
