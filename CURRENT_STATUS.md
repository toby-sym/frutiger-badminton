# Current status

- Project version is Unity 6000.0.64f1 with URP 17.0.4.
- The editor bootstrap generates the Aero Court scene and URP assets from C# using procedural meshes, primitives, and materials.
- The local practice loop is implemented with WASD movement, jump, dash, aimed clear, drop, drive, smash, scoring, and a returning rival.
- GitHub Actions now uses GameCI to build and package a portable Windows x64 Development player on every branch push. Run `44829318980` succeeded, including GameCI license activation, Windows build, zip packaging, and artifact upload.
- The user is smoke-testing that artifact. Assistant-side visual inspection is still pending; the local Unity Editor does not accept the supplied ULF on this cloud machine, although GameCI can activate the CI runner from the configured repository secrets.
- Current increment adds procedural body pivots and responsive run, jump, dash, and racket-swing animation. The next GameCI build will verify these changes.
- No imported 3D models or image textures have been added. No separate test suite has been added.
