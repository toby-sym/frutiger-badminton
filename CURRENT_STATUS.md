# Current status

- Project version is Unity 6000.0.64f1 with URP 17.0.4.
- The editor bootstrap generates the Aero Court scene and URP assets from C# using procedural meshes, primitives, and materials.
- The local practice loop is implemented with WASD movement, jump, dash, aimed clear, drop, drive, smash, scoring, and a returning rival.
- GitHub Actions uses GameCI to build and package a portable Windows x64 Development player on every branch push. Unity's `Library` folder is cached in development and release workflows, and superseded builds on the same branch are canceled.
- The impact feedback Windows build passed in run `44836155335` (10m) and uploaded its artifact. The direct merge to `main` passed in run `44837552946` (10m20s) and saved a 746 MB `Library` cache for the default branch.
- Procedural body pivots, responsive run/jump/dash/racket-swing animation, and generated shock rings and sparks at racket contact are implemented.
- Controller-first gamepad mappings and keyboard/mouse fallback are built with both input backends enabled from startup in `ProjectSettings/ProjectSettings.asset`. GameCI run `44843252147` passed in 8m39, uploaded a Windows artifact, and saved a compatible 765 MB Unity Library cache. Verify gamepad behavior in the artifact.
- Incoming shots now display a generated segmented landing reticle that follows the shuttle's predicted landing spot and pulses from aqua to coral as impact approaches. The HUD explains the cue. Fixed shuttle setup to assign its generated feather mesh directly instead of retrieving it from a mixed object list; artifact verification is pending.
- Capability branch `capability/ubuntu-gameci-development` moves development builds to GameCI's Ubuntu host with a Mono Windows target, avoiding the slow Windows editor image download. Cold run `44846946166` passed in 4m17s (3m55s for the Unity build), uploaded the Windows artifact, and saved a 719 MiB Linux `Library` cache. Two warm runs, `44847472993` and `44847870347`, passed in 2m34s and 3m10s total (2m11s and 2m43s for Unity), 53–62% faster than the previous warm Windows run at 6m47s. The release workflow remains on Windows.
- Assistant-side visual inspection is still pending. The user is smoke-testing an earlier artifact; the local Unity Editor does not accept the supplied ULF on this cloud machine, while GameCI activates its runner from configured repository secrets.
- No imported 3D models or image textures have been added. No separate test suite has been added.
