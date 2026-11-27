In the shooter kit's game flow (Core, Runtime, NetV2), publish typed state to `GameSignals` and one-off events through `CombatEvents`, and build log lines in `*Log` classes.

In the Demo sample's UI (`Samples~/Demo`):

- Turn `GameSignals` values and `ModeStatus.Detail` into text there (mode HUD text in a formatter registered with `ModeHudFormatters`).
- Put player-facing text in the sample's `Resources/Localization/<language>/*.json` with the same keys in every language, and read it through `GameText` / `LocalizedTexts`.
