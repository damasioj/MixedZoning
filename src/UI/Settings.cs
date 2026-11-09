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
