# Transcode & Replace — Jellyfin Plugin

Batch-transcodes your Jellyfin library to a target codec (HEVC / AV1 / H.264) and
**replaces the original file in place**, with automatic hardware detection, a
persistent job queue, verification gates and HDR preservation.

> ⚠️ **Replacing originals can lose data if done carelessly.** This plugin is built
> safety-first: it defaults to **dry-run**, never touches a file unless every
> verification gate passes, keeps a backup until verified, and replaces atomically
> within the same filesystem. See [Safety](#safety).

## Status

This repository is being built in milestones (see the architecture plan). Current state:

| Milestone | Scope | State |
|---|---|---|
| **M0** | Plugin shell, config, config page, DI | ✅ |
| **M1** | Hardware probe (real probe-encode, cached) | ✅ |
| **M2** | Persistent JSON job queue + discovery task | ✅ |
| **M3** | ffmpeg argument builder + **dry-run** worker | ✅ |
| M4 | Verifier (ffprobe gates) | ⏳ |
| M5 | Atomic replace + backup + permissions + refresh | ⏳ |
| M6 | Playback / idle / disk guards | ⏳ |
| M7 | HDR / Dolby Vision handling | ⏳ |
| M8 | VMAF gate (sample-based) | ⏳ |
| M9 | Status UI, resume hardening | ⏳ |

**M3 writes nothing.** In dry-run it logs the exact ffmpeg command per file. With
dry-run off it still refuses to encode/replace, because the verify + atomic-replace
pipeline (M4–M5) is intentionally not wired up yet. The destructive path will only
exist once it can be verified.

## How it works

```
DiscoveryTask (IScheduledTask)  ── scans libraries, enqueues jobs ──►  JsonJobQueue (persistent)
                                                                              │
TranscodeWorker (BackgroundService) ── drains queue ──────────────────────────┘
        ├─ HardwareProbe   real probe-encode per encoder, cached
        ├─ ArgBuilder      deterministic ffmpeg args (encoder × HDR × container)
        └─ FfmpegRunner    (dry-run: log only)
```

- **Hardware detection** does not trust compiled-in encoder lists. It runs a 1-second
  `testsrc` probe-encode per candidate and only keeps encoders that exit 0.
- **The queue is a single JSON file** in the plugin data folder — no external database,
  no extra native assemblies to deploy. Writes are atomic (`*.tmp` then replace). On
  restart, in-flight jobs reset to `Pending` (crash resume).
- **Idempotent**: a file already in the target codec (and above the bitrate floor) is
  not enqueued.

## Configuration

Dashboard → Plugins → **Transcode & Replace**.

| Setting | Default | Purpose |
|---|---|---|
| Dry-run | **on** | Simulate only; never writes |
| Target video codec | HEVC | h264 / hevc / av1 |
| Quality (CRF/CQ) | 23 | Lower = better/larger |
| Quality vs. speed | Quality | Software (libx265) vs. hardware first |
| Encoder preference | Auto | Auto / ForceSoftware / ForceHardware / Specific |
| Max parallel jobs | 1 | Keep at 1 for GPU encoders |
| Audio | Copy | Copy / +AAC / Transcode |
| Container | Keep | Keep / mkv / mp4 (mp4 falls back to mkv for image subs) |
| Min source bitrate | 0 | Only process "fat" files |
| Skip if already target codec | true | Idempotency |
| Replace policy | BackupThenDelete | vs. SideBySide / HardReplace |
| Backup retention (days) | 7 | |
| VMAF gate / threshold | off / 95 | Quality regression guard |
| Skip HDR / Dolby Vision | off / **on** | DV is not generically preservable |
| Run only when idle | true | Don't compete with playback |

## Safety

1. **Dry-run by default** — first runs only report.
2. **Verification gates before any replace** (M4): ffmpeg exit 0, plausible size,
   valid container, duration within ±0.5 %, expected stream counts; optional VMAF (M8).
3. **Backup until verified** — hard delete is opt-in only.
4. **Atomic move within the same filesystem** — never a half-written file at the
   original path; the temp file is written in the source directory.
5. **HDR10/HLG preserved, Dolby Vision skipped** — never silently degrades to SDR.
6. **In-use protection** — never replaces a file that is currently streaming (M6).

## Build

Requires the .NET 8 SDK.

```bash
dotnet build -c Release
dotnet test  -c Release
```

The plugin DLL lands in
`Jellyfin.Plugin.TranscodeReplace/bin/Release/net8.0/Jellyfin.Plugin.TranscodeReplace.dll`.

## Install (manual)

1. Build (above) or download the zip from a release.
2. Copy `Jellyfin.Plugin.TranscodeReplace.dll` into a folder under your Jellyfin
   `plugins` directory, e.g. `plugins/Transcode & Replace/`.
3. Restart Jellyfin. Configure the plugin and **leave dry-run on for the first run.**

Targets Jellyfin `10.10.x` (ABI `10.10.0.0`).

## License

MIT — see [LICENSE](LICENSE).
