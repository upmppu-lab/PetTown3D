PetTown3D v13 - PNG HUD graphics integration

This package preserves the v12 project and adds 16 original transparent PNG HUD assets under Assets/PetTown3D/Resources/HUD.
PetTownGame.cs loads the sprites through Unity Resources.Load and displays them as actual textures, not procedural flat-colored rectangles.
9:16 portrait mode activates the mobile HUD; landscape retains the original desktop HUD.
Existing game mechanics, prefabs, models and animation files were not intentionally modified.

IMPORTANT: This is a source-level update. Unity Editor compilation and on-device interaction have NOT been tested. Back up your project before replacing files.

How to test:
1. Back up the existing project.
2. Extract Assets, Packages, ProjectSettings into the project root.
3. In Unity choose Pet Town 3D > Open Playable Scene.
4. Game view: set 1080 x 1920; press Play.
5. Check Console for compile/runtime errors and verify all buttons.

New graphics are original approximations inspired by the provided visual reference, not exact extracted sprites.
