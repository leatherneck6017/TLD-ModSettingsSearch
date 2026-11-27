using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace ModSettingsSearch
{
    // Ctrl+F from the main menu, pause menu or any Options tab: open Options if needed, switch to the
    // Mod Settings tab, then open the popup. Each step takes effect a frame or more later, so it's driven
    // from OnUpdate until the tab shows up or we give up.
    internal static class Shortcut
    {
        private static int deadline = -1;
        private static bool tabClicked;

        internal static void Update()
        {
            if (deadline >= 0)
                Advance();

            if (!(KeyBlock.Held(KeyCode.LeftControl) || KeyBlock.Held(KeyCode.RightControl)) || !KeyBlock.Down(KeyCode.F))
                return;
            // someone is typing somewhere (save name, CraftingSearch, our own box)
            if (SearchPopup.IsOpen || UIInput.selection != null)
                return;

            if (InterfaceManager.IsPanelEnabled<Panel_OptionsMenu>())
                Start();
            else if (InterfaceManager.IsPanelEnabled<Panel_PauseMenu>())
            {
                InterfaceManager.GetPanel<Panel_PauseMenu>().OnOptions();
                Start();
            }
            else if (InterfaceManager.IsPanelEnabled<Panel_Sandbox>())
            {
                InterfaceManager.GetPanel<Panel_Sandbox>().OnClickOptions();
                Start();
            }
            else if (InterfaceManager.IsPanelEnabled<Panel_MainMenu>())
            {
                Panel_MainMenu main = InterfaceManager.GetPanel<Panel_MainMenu>();
                if (main.IsSubMenuEnabled())
                    return;
                if (ClickItem(main.m_BasicMenu, item => Matches(item.m_Id) || Matches(item.m_LabelText)))
                    Start();
                else
                    ModSettingsSearchMod.WarnOnce("mainMenuOptions", "Couldn't find the Options item on the main menu.");
            }
        }

        private static bool Matches(string text) => text != null && text.Contains("option", StringComparison.OrdinalIgnoreCase);

        private static void Start()
        {
            deadline = Time.frameCount + 120;
            tabClicked = false;
        }

        private static void Advance()
        {
            if (Time.frameCount > deadline)
            {
                deadline = -1;
                return;
            }

            if (!InterfaceManager.IsPanelEnabled<Panel_OptionsMenu>())
                return;

            Panel_OptionsMenu options = InterfaceManager.GetPanel<Panel_OptionsMenu>();
            GameObject tab = ModSettingsAccess.GetTab(options);
            if (tab == null)
                return;

            if (tab.activeInHierarchy)
            {
                SearchPopup.RequestOpen();
                deadline = -1;
            }
            else if (!tabClicked)
            {
                // the "Mod Settings" entry ModSettings adds to the options list (id "ModSettings")
                tabClicked = true;
                if (!ClickItem(options.m_BasicMenu, item => item.m_Id == "ModSettings"))
                {
                    ModSettingsSearchMod.WarnOnce("modSettingsItem", "Couldn't find the Mod Settings entry in Options.");
                    deadline = -1;
                }
            }
        }

        private static bool ClickItem(BasicMenu menu, System.Func<BasicMenu.BasicMenuItemModel, bool> match)
        {
            if (menu == null)
                return false;
            foreach (BasicMenu.BasicMenuItemModel item in menu.m_ItemModelList)
            {
                if (item != null && item.m_Action != null && match(item))
                {
                    item.m_Action.Invoke();
                    return true;
                }
            }
            return false;
        }
    }
}
