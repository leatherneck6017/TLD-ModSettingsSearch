using System.Reflection;
using HarmonyLib;
using Il2Cpp;

namespace ModSettingsSearch
{
    [HarmonyPatch(typeof(Panel_OptionsMenu), "Update")]
    internal static class OptionsMenuUpdate
    {
        private static void Postfix(Panel_OptionsMenu __instance) => SearchPopup.Tick(__instance);
    }

    [HarmonyPatch(typeof(Panel_OptionsMenu), nameof(Panel_OptionsMenu.OnCancel))]
    internal static class OptionsMenuCancel
    {
        private static bool Prefix() => !SearchPopup.BlocksMenuInput;
    }

    [HarmonyPatch(typeof(Panel_OptionsMenu), nameof(Panel_OptionsMenu.Enable), [typeof(bool)])]
    internal static class OptionsMenuEnable
    {
        private static void Postfix(bool enable)
        {
            if (!enable)
                SearchPopup.ForceClose();
        }
    }

    // ModSettingsGUI.Update does the page's arrow/stick navigation, mouse-wheel scrolling and its own
    // Esc -> OnCancel, none of which should happen behind the popup.
    [HarmonyPatch]
    internal static class ModSettingsGuiUpdate
    {
        private static bool Prepare() => TargetMethod() != null;

        private static MethodBase TargetMethod() =>
            ModSettingsAccess.GuiType != null ? AccessTools.Method(ModSettingsAccess.GuiType, "Update") : null;

        private static bool Prefix() => !SearchPopup.BlocksMenuInput;
    }
}
