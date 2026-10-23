PetTown 3D v04 - Joystick display and close-up camera zoom

1. Exit Unity Play mode.
2. Back up the Unity project.
3. Copy the Assets folder into the Unity project root, replacing the two scripts.
4. Wait for Unity compilation and press Play. No installer rerun.

Changes: GUI.depth=-1000 to draw circular joystick above existing mobile HUD. Camera orthographic zoom minimum 0.6 (previously 10); proportional mouse-wheel zoom and two-finger pinch. Existing character, animations, controller and scene are unchanged.

Note: joystick position may overlap existing HUD bottom-left buttons; test in Game view and report a screenshot if repositioning is needed.
