using System.Reflection;
using Il2Cpp;
using UnityEngine;

namespace ModSettingsSearch
{
    internal static class ModSettingsAccess
    {
        // ModSettingsGUI is internal, so it's reached by reflection. Resolved in the static constructor because
        // MelonLoader applies the Harmony patches (which need the type) before OnInitializeMelon runs.
        internal static readonly System.Type GuiType = typeof(ModSettings.ModSettingsBase).Assembly.GetType("ModSettings.ModSettingsGUI");
        private static readonly FieldInfo selectedIndex = GuiType?.GetField("selectedIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo getGui = GuiType != null ? typeof(Component).GetMethod("GetComponent", System.Type.EmptyTypes)?.MakeGenericMethod(GuiType) : null;

        // the tab ModSettings clones from the Quality tab; the mod picker is the old Quality combo box
        internal static GameObject GetTab(Panel_OptionsMenu panel) => panel.transform.Find("Pages/ModSettings")?.gameObject;

        internal static ConsoleComboBox GetSelector(GameObject tab) => tab?.transform.Find("GameObject/Quality")?.GetComponent<ConsoleComboBox>();

        // row ModSettings has highlighted for keyboard/controller navigation, 0 is the mod picker
        internal static int GetSelectedRow(GameObject tab)
        {
            if (selectedIndex == null || getGui == null)
                return -1;
            try
            {
                object gui = getGui.Invoke(tab.transform, null);
                return gui != null ? (int)selectedIndex.GetValue(gui) : -1;
            }
            catch (System.Exception ex)
            {
                ModSettingsSearchMod.WarnOnce("selectedIndex", "Could not read the ModSettings selected row: " + ex.Message);
                return -1;
            }
        }

        // ModSettings already limits the picker's items to mods with a visible page and sorts them with
        // List<string>.Sort() on the Il2Cpp side (culture compare, so "[Tiny Tweaks]" comes first).
        // Copy that order as-is rather than sorting again.
        internal static List<string> GetModNames(ConsoleComboBox selector)
        {
            var names = new List<string>();
            foreach (string name in selector.items)
                if (!string.IsNullOrEmpty(name))
                    names.Add(name);
            return names;
        }

        // Same path as the arrow buttons: the value setter doesn't fire onChange, and ModSettings wired
        // onChange to SelectMod(modSelector.value).
        internal static void SelectMod(ConsoleComboBox selector, string mod)
        {
            if (selector.value == mod)
                return;
            selector.value = mod;
            EventDelegate.Execute(selector.onChange);
        }
    }
}
