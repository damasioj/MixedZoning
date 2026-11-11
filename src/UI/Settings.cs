using ColossalFramework.UI;
using ICities;
using MixedZoning.Config;
using UnityEngine;

namespace MixedZoning.UI
{
    internal static class Settings
    {
        /// <summary>Persist the config and push the change to anything showing it in-game.</summary>
        internal static void Changed()
        {
            MixConfig.Save();
            // Only tooltips/enabled state depend on the config; colours and patterns are fixed.
            if (Loading.InGame) Patches.ZoningPanel_RefreshPanel.UpdateButtons();
        }

        public static void Build(UIHelperBase helper)
        {
            var cfg = MixConfig.Current;

            // When a zone allows both densities of a type: random by default, or a demand threshold.
            var density = helper.AddGroup("Low vs high density (zones that allow both)");
            UISlider slider = null;
            UILabel sliderLabel = null;
            density.AddCheckbox("Use a demand threshold (off: pick low or high at random)", cfg.UseHighDensityThreshold, on =>
            {
                cfg.UseHighDensityThreshold = on;
                if (slider != null) slider.parent.isEnabled = on;
                Changed();
            });
            slider = density.AddSlider(ThresholdText(cfg.HighDensityDemandThreshold), 0, 100, 5, cfg.HighDensityDemandThreshold, value =>
            {
                cfg.HighDensityDemandThreshold = Mathf.RoundToInt(value);
                if (sliderLabel != null) sliderLabel.text = ThresholdText(cfg.HighDensityDemandThreshold);
                Changed();
            }) as UISlider;
            if (slider != null)
            {
                sliderLabel = slider.parent.Find<UILabel>("Label");
                slider.parent.isEnabled = cfg.UseHighDensityThreshold;
            }

            foreach (var mix in cfg.Mixes)
            {
                AddMixCheckboxes(helper.AddGroup(MixedZones.DisplayName(mix.Slot)), mix);
            }

            AddDeveloperUpload(helper);
        }

        /// <summary>Workshop item this mod is published as.</summary>
        private const ulong WorkshopId = 3836655786UL;

        /// <summary>
        /// Developer-only: on a machine with the local mod folder (Addons/Mods/MixedZoning), offer to
        /// upload that folder as an update to the published Workshop item. This opens the game's own
        /// upload window, the same one its Content Manager "Update" button opens; Steam only accepts
        /// it from the item's owner. Subscribers don't have the local folder, so they never see it.
        /// </summary>
        private static void AddDeveloperUpload(UIHelperBase helper)
        {
            string localFolder;
            try
            {
                localFolder = System.IO.Path.Combine(ColossalFramework.IO.DataLocation.modsPath, "MixedZoning");
                if (!System.IO.Directory.Exists(localFolder)) return;
            }
            catch
            {
                return;
            }

            var dev = helper.AddGroup("Developer (only shown with the local mod folder)");
            dev.AddButton($"Upload local folder as update to Workshop item {WorkshopId}", () =>
            {
                try
                {
                    var panel = UIView.library.ShowModal<WorkshopModUploadPanel>("WorkshopModUploadPanel");
                    if (panel != null) panel.SetAsset(localFolder, new ColossalFramework.PlatformServices.PublishedFileId(WorkshopId));
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[MixedZoning] Could not open the Workshop upload window: {e}");
                }
            });
        }

        private static string ThresholdText(int value) =>
            $"High density once demand reaches {value} (low density below it)";

        /// <summary>The six building-type toggles for one mix. Shared by the options screen and the in-game window.</summary>
        internal static void AddMixCheckboxes(UIHelperBase helper, ZoneMix m)
        {
            helper.AddCheckbox("Low-density residential", m.ResidentialLow, v => { m.ResidentialLow = v; Changed(); });
            helper.AddCheckbox("High-density residential", m.ResidentialHigh, v => { m.ResidentialHigh = v; Changed(); });
            helper.AddCheckbox("Low-density commercial", m.CommercialLow, v => { m.CommercialLow = v; Changed(); });
            helper.AddCheckbox("High-density commercial", m.CommercialHigh, v => { m.CommercialHigh = v; Changed(); });
            helper.AddCheckbox("Industrial", m.Industrial, v => { m.Industrial = v; Changed(); });
            helper.AddCheckbox("Office", m.Office, v => { m.Office = v; Changed(); });
        }
    }
}
