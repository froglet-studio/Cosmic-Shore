# Controller Configurator

## Placement

The controller configurator is hosted by the existing `SettingsModal`. This keeps controller mapping with global player settings instead of profile cosmetics, and it also makes the configurator reachable anywhere the pause menu opens settings.

## Presets

The runtime uses Unity Input System `Gamepad` controls as its normalized layer. Most modern controllers map to the same physical control sources:

- Xbox / Windows
- iOS / macOS MFi
- Steam Deck
- Android Generic

Nintendo Switch is the exception this first pass handles explicitly. It keeps the same stick, shoulder, and trigger sources, but swaps the face-button semantics so the Nintendo east/south face-button labels match the player expectation.

## Custom Mapping

The configurator can capture each gameplay binding from the connected `Gamepad.current`:

- Left Stick
- Right Stick
- Button 1
- Button 2
- Button 3
- Left Trigger
- Right Trigger
- Throttle
- Flip

Custom mappings are saved locally through `PlayerPrefs` for immediate reuse, then exported into `PlayerSettingsCloudData.ControllerMappingJson` through `GameSetting` so mappings can roam with the player's settings when cloud save is available.

## Unity Test Plan

1. Open `Menu_Main` and verify the Settings modal includes a `Controller` button.
2. Open the pause menu in a gameplay scene and verify the same Settings modal exposes the configurator.
3. Connect an Xbox-style controller and confirm the `Xbox / Windows` preset preserves existing flight controls.
4. Select `Nintendo Switch` and confirm face-button actions follow Nintendo label expectations.
5. Use `Map All`, move/press each requested control, restart play mode, and confirm the custom mapping persisted.
6. Sign in with cloud services initialized, change a mapping, and confirm `PlayerSettingsCloudData.ControllerMappingJson` is populated.
