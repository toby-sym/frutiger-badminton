# Development workflow

- Keep `main` for development and `release` for production. Never commit directly to `main` or `release`.
- Start each distinct change from the latest `origin/main` on a focused `feature/`, `story/`, `capability/`, or `defect/` branch. Publish the branch before editing; do not accumulate unrelated changes on one branch.
- If a change depends on an unmerged published branch, create a stacked branch from the latest `origin/main` and merge that dependency branch before editing. Keep the dependency visible; do not rewrite or duplicate its commits.
- Commit in small, logical increments with descriptive English messages. Do not use conventional commit prefixes.
- Run relevant build checks before merging through repository protections. Do not bypass protections.
- The Windows x64 development player must be built and uploaded as a branch-and-SHA named Actions artifact on every push.
- Production version bumps and releases are manual only, from `main`; do not promote ordinary merges automatically.
- Use the existing checkout in `/workspace`; do not create another worktree unless requested.
- Preserve Unity `.meta` files. Do not commit generated player output.
- Use procedural meshes, shaders, materials, and geometry for game visuals. Do not add imported 3D models or image textures.
