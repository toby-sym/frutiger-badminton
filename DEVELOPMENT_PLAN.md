# Development plan

1. **In progress — cloud court presentation:** the procedural island, sky, clouds, badminton court, lighting, and net are in place. Cloud banks now drift gently and glass lanterns bob and orbit; finish and visually inspect the court through the artifact smoke test.
2. **Implemented — character and shuttle presentation:** the generated shuttlecock, trail, responsive character/racket animation, procedural contact rings and sparks, and generated double-sided racket hoop with crossed string bed are in place. Visual inspection remains pending.
3. **In progress — controller-first local rally:** gamepad movement, aiming, jump, dash, and shot mappings are implemented alongside keyboard and mouse fallback. Incoming shots now use a generated, animated landing reticle to make movement decisions easier to read. The shuttle's generated feather mesh is also assigned directly during construction. Verify the controller path, landing cue, and aim response in the artifact.
4. Add AI teammates and run-based solo upgrades.
5. Add competitive online 3v3 after the local rally is solid.
6. Continue improving menus, feedback, presentation, and replayability.

Keep the visual direction procedural: use generated geometry, materials, and shaders without imported 3D models or image textures. Build and inspect each increment before moving on.
