# Changelog

All notable changes to **Mod Settings Search** will be documented in this file.

## [1.0.0] - 27-Nov-26

### Added

- **Search** button next to the mod picker on the Mod Settings tab (Options > Mod Settings). It opens a popup with a search box and a scrollable list of every mod that has a settings page.
- The list uses the same order as the mod picker. It opens with the list active (arrow keys and Enter work straight away); press Tab or click the search box to type.
- The list narrows as you type. Names that start with what you typed come first, and spaces are ignored, so `itemweight` finds "Item Weight Tweaker".
- Clicking a name, or choosing it with Up/Down and pressing Enter, switches straight to that mod's page through the normal mod picker.
- The page you're on is shown in gold, and the list starts on it.
- Also opens with Enter / controller A while the mod picker row is selected, or with Ctrl+F from the main menu, the pause menu or any Options tab (it switches to Mod Settings first).
- The scroll bar can be dragged, and clicking above or below it pages the list.
- Game and mod hotkeys (InputManager.GetKeyDown and Input.GetKey/GetKeyDown/GetKeyUp) are blocked while the popup is open, so typing doesn't trigger other mods' keybinds. Esc (or controller B) closes only the popup, not the options menu.
- Built from the game's own NGUI widgets and fonts, with a solid, opaque popup.
- Ships as a MelonLoader subfolder mod (`Mods\ModSettingsSearch\ModSettingsSearch.dll` plus `manifest.json`).
