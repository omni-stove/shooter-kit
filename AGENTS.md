Work the game engine by `.agents/refs/scriptability-rules.md`.

Leave existing `.meta` files untouched.

Write scripts in sh.

Write tests and their expected values with each change, but do not run the Unity tests.

Write a code comment with `//`, only to say why the code is not written another way.

This repo is game-kit: `Assets/kits/core` holds code any genre would use, and each other kit holds one genre's code on top of it.

Each game replaces the kits' content. In kit code:

- Put a value a game would tune (stats, rules, movement, enemy AI, scores and timers) in data (JSON / Spec / ScriptableObject), with only its default in code.
- Read the configured value, not the `Default*` constant.
- Take visuals and sounds from a replaceable reference; build shapes in code only when it is empty.
- Keep kit game flow free of display text and of UI types, and keep asset display names as plain key strings in data.
- Look up content by Id or data, never by its name.
