# Source and build references

The complete LumaShot source for a release is attached to the same GitHub release and available at its tag. Build using `bootstrap-windows.ps1`, then `build-installer.ps1`. `bootstrap-windows.ps1` pins download URLs and SHA-256 digests; npm dependencies are pinned by `backend/package-lock.json`.

The installer includes separate, unmodified upstream programs. Their source is available at these locations alongside the binary download information:

- ShareX 22.4.0: [source tree and build projects](https://github.com/ShareX/ShareX/tree/v22.4.0), [source archive](https://github.com/ShareX/ShareX/archive/refs/tags/v22.4.0.zip), [upstream release](https://github.com/ShareX/ShareX/releases/tag/v22.4.0). All component licenses remain in `engine/Licenses`.
- FFmpeg, supplied by that ShareX portable release, `n9.0.2-48-gd4070a14ce-20261107`: [source commit](https://github.com/FFmpeg/FFmpeg/tree/d4070a14cecc7e29eb278f9b6036c2284255f70d), [matching Windows build scripts and dependency source references](https://github.com/FFmpeg-Builds/FFmpeg-Builds/tree/c63bb308a1de656b425c132a8102f8fd5389afc5), [upstream build](https://github.com/FFmpeg-Builds/FFmpeg-Builds/releases/tag/autobuild-2026-11-07-12-47). Build configuration can be inspected with `engine/ffmpeg.exe -buildconf`. License: `engine/Licenses/FFmpeg_license.txt`. The FFmpeg source and build-script archives are mirrored in this release; dependency fetch/build scripts are in `scripts.d`.
- rclone 1.76.0: [source and build instructions](https://github.com/rclone/rclone/tree/v1.76.0), [upstream release](https://github.com/rclone/rclone/releases/tag/v1.76.0).
- Node 24.22.3: [source distribution](https://nodejs.org/dist/v24.22.3/node-v24.22.3.tar.gz); notices in `runtime/LICENSE.txt`.
- The optional ChatGPT DevKit source is included in `backend/siwc`, pinned to OpenAI commit `0a36fefeb913055c8c7a1a29b63d82b96b2e841a`, with its own noncommercial license. `PATCHES.md` and changed-file comments identify local modifications. It is not covered by LumaShot's GPL license.

The runtime window presentation changes are implemented entirely in LumaShot's `EngineBranding.cs`; no ShareX or FFmpeg executable is patched.
