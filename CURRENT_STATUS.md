# Current status

- Project version is Unity 6000.0.64f1 with URP 17.0.4.
- The editor bootstrap generates the Aero Court scene and URP assets from C# using procedural meshes, primitives, and materials.
- The local practice loop is implemented with WASD movement, jump, dash, aimed clear, drop, drive, smash, scoring, and a returning rival.
- GitHub Actions uses GameCI to build and package a portable Windows x64 Development player on every branch push. Runs `44829318980` and `44832487294` succeeded, including license activation, Windows builds, zipping, and artifact uploads.
- The user is smoke-testing the earlier artifact. Assistant-side visual inspection is still pending; the local Unity Editor does not accept the supplied ULF on this cloud machine, while GameCI activates its runner from the configured repository secrets.
- Procedural body pivots and responsive run, jump, dash, and racket-swing animation are implemented; GameCI run `44832487294` verified the Windows player and uploaded its artifact.
- Capability branch `capability/court-impact-feedback` adds a generated shock ring and glass-like sparks on racket contact. Its GameCI build is pending.
- No imported 3D models or image textures have been added. No separate test suite has been added.
