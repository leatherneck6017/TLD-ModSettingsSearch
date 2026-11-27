using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace ModSettingsSearch
{
    // Turning Rewired off isn't enough: most TLD mods read their keybinds with InputManager.GetKeyDown or
    // UnityEngine.Input.GetKeyDown in OnUpdate, which keep running while the game is paused. These patches
    // make those return false while the popup is up. UIInput takes its text from OnGUI events, so typing
    // still works. Our own key checks go through Down/Held, which skip the block.
    internal static class KeyBlock
    {
        private static bool bypass;

        private static bool Blocked => !bypass && SearchPopup.BlocksKeys;

        internal static bool Down(KeyCode key)
        {
            bypass = true;
            try { return Input.GetKeyDown(key); }
            finally { bypass = false; }
        }

        internal static bool Held(KeyCode key)
        {
            bypass = true;
            try { return Input.GetKey(key); }
            finally { bypass = false; }
        }

        private static bool Prefix(ref bool __result)
        {
            if (!Blocked)
                return true;
            __result = false;
            return false;
        }

        // patched one by one so a failure on one method doesn't take the others down with it
        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            var prefix = new HarmonyMethod(AccessTools.Method(typeof(KeyBlock), nameof(Prefix)));
            var targets = new[]
            {
                AccessTools.Method(typeof(InputManager), nameof(InputManager.GetKeyDown), [typeof(MonoBehaviour), typeof(KeyCode)]),
                AccessTools.Method(typeof(Input), nameof(Input.GetKeyDown), [typeof(KeyCode)]),
                AccessTools.Method(typeof(Input), nameof(Input.GetKey), [typeof(KeyCode)]),
                AccessTools.Method(typeof(Input), nameof(Input.GetKeyUp), [typeof(KeyCode)]),
            };

            foreach (var target in targets)
            {
                try
                {
                    harmony.Patch(target, prefix: prefix);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Warning($"Couldn't patch {target?.DeclaringType?.Name}.{target?.Name}, keys may reach other mods while typing: {ex.Message}");
                }
            }
        }
    }
}
