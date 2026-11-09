using ColossalFramework;
using ICities;
using MixedZoning.Config;
using MixedZoning.UI;
using MixedZoning.Zones;
using UnityEngine;

namespace MixedZoning
{
    public class Loading : LoadingExtensionBase
    {
        public static bool InGame { get; private set; }

        private static Color[] originalZoneColors;

        public override void OnLevelLoaded(LoadMode mode)
        {
            InGame = mode == LoadMode.LoadGame || mode == LoadMode.NewGame || mode == LoadMode.NewGameFromScenario;
            if (!InGame) return;
            ApplyZoneColors();
            ZoningPanel panel = Object.FindObjectOfType<ZoningPanel>();
            if (panel != null) panel.RefreshPanel();
        }

        public override void OnLevelUnloading()
        {
            InGame = false;
            MixEditorWindow.DestroyWindow();
            MixedZoneIcons.Destroy();
            RestoreZoneColors();
        }

        /// <summary>
        /// Fixed colours, set once per city load and never changed by configuration:
        ///  - slot 1 ("Distant", never stored in zone cells) becomes the light purple, both in the
        ///    array and the shader global the overlay reads;
        ///  - slots 8..14 are added so ZoneTool can colour the brush (it indexes m_zoneColors by
        ///    zone). This is a scene property and never saved; the shader only reads slots 0..7.
        /// </summary>
        private static void ApplyZoneColors()
        {
            ZoneProperties props = Singleton<ZoneManager>.exists ? Singleton<ZoneManager>.instance.m_properties : null;
            if (props?.m_zoneColors == null || props.m_zoneColors.Length < 8) return;

            if (originalZoneColors == null) originalZoneColors = (Color[])props.m_zoneColors.Clone();

            var colors = new Color[MixedZones.First + MixedZones.Count];
            System.Array.Copy(originalZoneColors, colors, 8);
            colors[(int)MixedZonePatterns.PurpleSlot] = MixedZonePatterns.Purple;
            props.m_zoneColors = colors;   // BrushColor reads vanilla colours from here
            for (int slot = 0; slot < MixedZones.Count; slot++) colors[MixedZones.First + slot] = MixedZoneIcons.BrushColor(slot);
            Shader.SetGlobalColor("_ZoneColor" + (int)MixedZonePatterns.PurpleSlot, MixedZonePatterns.Purple.linear);
        }

        private static void RestoreZoneColors()
        {
            if (originalZoneColors == null) return;
            ZoneProperties props = Singleton<ZoneManager>.exists ? Singleton<ZoneManager>.instance.m_properties : null;
            if (props != null)
            {
                props.m_zoneColors = originalZoneColors;
                int slot = (int)MixedZonePatterns.PurpleSlot;
                Shader.SetGlobalColor("_ZoneColor" + slot, originalZoneColors[slot].linear);
            }
            originalZoneColors = null;
        }
    }
}
