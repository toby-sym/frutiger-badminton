# Current status

- Repository initialized as a Unity 6.0.64f1 project using URP 17.0.4.
- First procedural Aero Court increment is implemented in `Assets/Scripts/Runtime/AeroCourtScene.cs`; the Unity editor bootstrap generates the scene and URP assets on first open.
- A batch-mode Windows x64 Development build method and PowerShell packaging script are present.
- Push and manual release workflows are present. Their Unity build is blocked until a runner has Unity 6000.0.64f1 with Windows Standalone Build Support and a valid Unity license.
- This environment has no Unity editor, so scene import, compilation, build, and visual inspection have not been verified. No test suite was added or run.
- Next: open the project in the pinned Unity editor, fix any import/runtime issues, inspect the scene, then add the moving rally loop.
