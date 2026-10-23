Pet Town 3D - Player Controls Fix v02

FIXES
- WASD/Shift animation works but character stays in place: remove the blocking linecast and ground using the generated terrain collider only.
- Click-to-move: raycast against EDITABLE_Terrain_Mesh directly; fallback to horizontal plane.
- Joystick: now visible in Unity Editor Game view and on mobile. In Editor, the joystick is DISPLAY ONLY; mobile touch control works on a mobile build.

INSTALL
1. Stop Play mode. Back up your project.
2. Copy the included Assets folder into the Unity project root and allow replacement of PetTownPlayerControls.cs ONLY.
3. Wait for Unity compilation. Do not rerun the installer.
4. Press Play, select Game view, test WASD/Shift and click ground.

NOTES
- This is a targeted source patch, not a verified Unity runtime build.
- Does not modify PetTownGame.cs, scene, FBX, Animator, or UI assets.
- True obstacle avoidance still requires NavMesh setup.
- If the character moves but sinks/floats, ground offset and scene terrain heights need further adjustment.
