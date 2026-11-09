using ICities;
using MixedZoning.Config;

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

            var general = helper.AddGroup("Mixed Zoning");
            general.AddSlider("High-density demand threshold", 0, 100, 5, cfg.HighDensityDemandThreshold, value =>
            {
                cfg.HighDensityDemandThreshold = (int)value;
                Changed();
            });

            foreach (var mix in cfg.Mixes)
            {
                AddMixCheckboxes(helper.AddGroup(MixedZones.DisplayName(mix.Slot)), mix);
            }
        }

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
