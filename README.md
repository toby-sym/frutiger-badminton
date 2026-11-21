# Frutiger Badminton

An original arcade badminton project with a bright, glossy world above the clouds. The first increment builds its court, cloud banks, lighting, characters, rackets, and sky procedurally from Unity primitives, generated meshes, and a vertex-color shader. It uses no imported 3D models or image textures.

## Open and run

1. Install Unity Editor **6000.0.64f1** through Unity Hub. The project uses URP **17.0.4**; Unity Package Manager resolves it from `Packages/manifest.json` on first open.
2. In Unity Hub, choose **Add project from disk** and select this repository directory.
3. Wait for package resolution and script compilation. The editor bootstrap creates `Assets/Scenes/AeroCourt.unity` and the URP assets the first time the project opens.
4. Open the generated Aero Court scene and press **Play**.

## Controls

With a controller connected, use the **left stick or D-pad** to move and the **right stick** to aim. **A** jumps, **RB** dashes, **LB** clears, **X** drops, **B** drives, and **Y** smashes while airborne. Keyboard and mouse remain available: **WASD/arrows** move, **mouse** aims, **Space** jumps, **Shift** dashes, **left/right click** clear/drop, and **F/R** drive/smash.

## Windows development build

The batch build requires Unity `6000.0.64f1` with **Windows Build Support** installed. Set `UNITY_EDITOR_PATH` to the full path of `Unity.exe`, then run from the project root in PowerShell:

```powershell
./scripts/build-windows.ps1
```

The script builds a Development player folder and zips the executable with its `*_Data` runtime folder into `artifacts/`. GitHub Actions uses the open-source GameCI Unity Builder on Ubuntu to cross-build a Windows x64 Development player with Mono on every branch push. The manually triggered release workflow builds through GameCI on Windows. Both workflows need these repository Actions secrets under **Settings → Secrets and variables → Actions**:

- `UNITY_LICENSE`: full contents of the Unity `.ulf` file
- `UNITY_EMAIL`: email for the Unity account that owns the Personal license
- `UNITY_PASSWORD`: password for that Unity account

Keep the `.ulf` file out of the repository. GameCI uses Docker and activates the supplied Personal license for its build.

The manually triggered **Release Windows build** workflow runs from `main`, merges the latest `origin/main` into `release`, applies the selected semantic version bump (first release `0.1.0`), builds a non-development Windows player, and atomically pushes the release branch and annotated tag before creating a GitHub Release with generated notes and the zip.

See [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) and [CURRENT_STATUS.md](CURRENT_STATUS.md) for the ongoing work and current verification status.
