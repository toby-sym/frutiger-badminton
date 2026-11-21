# Development plan

1. **In progress — cloud court presentation:** the procedural island, sky, clouds, badminton court, lighting, and net are in place. Cloud banks drift gently, glass lanterns bob and orbit, and the court has an animated texture-free HLSL sheen; finish and visually inspect the court through the artifact smoke test.
2. **Implemented — character and shuttle presentation:** the generated shuttlecock, trail, responsive character/racket animation, procedural contact rings and sparks, generated racket string beds, and distinctive player kit details are in place. Visual inspection remains pending.
3. **In progress — controller-first local rally:** gamepad movement, aiming, jump, dash, and shot mappings are implemented alongside keyboard and mouse fallback. Players alternate aimed serves; a HUD dash meter now shows readiness and recharge, with a message on early attempts. Incoming shots use a separate animated landing reticle, and dashes leave an aqua trail and burst. Verify serving, controller paths, landing cues, dash feedback, and aim preview in the artifact.
4. **In progress — solo 3v3 and upgrades:** the player and two AI teammates face three generated opponents, with a team-colored reticle marking the assigned returner. Opponents mix clears, drops, and drives toward less-covered lanes, with shot-color trails. Solo runs are first to 7 by two points (cap 9), draft an upgrade every three player points, and offer replay with progression reset; balance the run.
5. Add competitive online 3v3 after the local rally is solid.
6. Continue improving menus, feedback, presentation, and replayability; points add a team-colored rally burst and score-panel pulse, and shot contacts add a brief screen-space camera shake that scales with shot strength.

Keep the visual direction procedural: use generated geometry, materials, and shaders without imported 3D models or image textures. Build and inspect each increment before moving on.
