using MelonLoader;

namespace ModSettingsSearch
{
    public class ModSettingsSearchMod : MelonMod
    {
        private static readonly HashSet<string> warned = new();

        internal static void WarnOnce(string id, string message)
        {
            if (warned.Add(id))
                MelonLogger.Warning(message);
        }

        public override void OnInitializeMelon()
        {
            KeyBlock.Apply(HarmonyInstance);
            if (ModSettingsAccess.GuiType == null)
                MelonLogger.Warning("ModSettings.ModSettingsGUI not found, Esc and arrow keys may leak through the popup.");
        }

        public override void OnUpdate() => Shortcut.Update();

        // never leave the game's input maps switched off across a scene change
        public override void OnSceneWasUnloaded(int buildIndex, string sceneName) => SearchPopup.ForceClose();
    }
}
