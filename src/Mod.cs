using CitiesHarmony.API;
using ICities;

namespace MixedZoning
{
    /// <summary>Entry point the game discovers via IUserMod.</summary>
    public class Mod : IUserMod
    {
        public const string HarmonyId = "com.mixedzoning.cs1";

        public string Name => "Mixed Zoning";

        public string Description => "Adds paintable zone types that grow a mix of residential, commercial, office and industrial buildings.";

        public void OnEnabled()
        {
            Config.MixConfig.Load();
            HarmonyHelper.DoOnHarmonyReady(Patches.Patcher.PatchAll);
        }

        public void OnDisabled()
        {
            if (HarmonyHelper.IsHarmonyInstalled)
            {
                Patches.Patcher.UnpatchAll();
            }
        }

        public void OnSettingsUI(UIHelperBase helper)
        {
            UI.Settings.Build(helper);
        }
    }
}
