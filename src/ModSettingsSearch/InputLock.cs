namespace ModSettingsSearch
{
    // From CraftingSearch: switch Rewired's maps off while typing so letters don't hit game hotkeys
    internal static class InputLock
    {
        private static bool s_locked;

        internal static void Set(bool locked)
        {
            if (s_locked == locked)
                return;
            s_locked = locked;

            try
            {
                var reInputType = System.Type.GetType("Il2CppRewired.ReInput, Il2CppRewired_Core");
                if (reInputType == null)
                {
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        reInputType = asm.GetType("Il2CppRewired.ReInput") ?? asm.GetType("Rewired.ReInput");
                        if (reInputType != null)
                            break;
                    }
                }

                if (reInputType == null)
                    return;

                var playersProp = reInputType.GetProperty("players");
                object players = playersProp?.GetValue(null);
                if (players == null)
                    return;

                var getPlayerMethod = players.GetType().GetMethod("GetPlayer", [typeof(int)]);
                object player = getPlayerMethod?.Invoke(players, [0]);
                if (player == null)
                    return;

                var controllersProp = player.GetType().GetProperty("controllers");
                object controllers = controllersProp?.GetValue(player);
                var mapsProp = controllers?.GetType().GetProperty("maps");
                object maps = mapsProp?.GetValue(controllers);

                var setMapsEnabled = maps?.GetType().GetMethod("SetMapsEnabled", [typeof(bool), typeof(int)]);
                setMapsEnabled?.Invoke(maps, [!locked, 0]);
            }
            catch (System.Exception ex)
            {
                ModSettingsSearchMod.WarnOnce("rewired", "Rewired lock error: " + ex.Message);
            }
        }
    }
}
