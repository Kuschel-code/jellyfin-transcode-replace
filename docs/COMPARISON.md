# How this compares to other transcoding tools

State of the field as of early 2026. This is an honest comparison, including where
this plugin is weaker. If you need scale or complex pipelines today, one of the mature
tools below is the better choice; this plugin's point is to do the common
"shrink my library in place" job from inside Jellyfin with no extra infrastructure.

## The tools

- **Tdarr** — server + worker nodes, distributed transcoding, a plugin/"flows" system,
  health checks, web UI. Runs as separate containers. The heavyweight of the space.
- **Unmanic** — open-source library optimizer, plugin system, web UI, scheduled scans,
  in-place replacement. Runs as a separate container.
- **FileFlows** — node-graph "flows" for media processing (and general file handling),
  web UI, libraries. Runs as a separate container. Very flexible.
- **HandBrake** (GUI / HandBrakeCLI) — manual or scripted batch encoding. No library
  integration, no auto-replace. Excellent encoder control.
- **Jellyfin built-in** — real-time transcoding for playback only. It never permanently
  re-encodes or replaces your files.
- **This plugin** — a Jellyfin plugin. No separate service. Scans the libraries, encodes
  with jellyfin-ffmpeg, verifies, and replaces the original in place.

## Feature matrix

| | This plugin | Tdarr | Unmanic | FileFlows | HandBrake |
|---|---|---|---|---|---|
| Deployment | Jellyfin plugin (no extra service) | Separate server + nodes | Separate container | Separate container | Desktop / CLI |
| Replace originals in place | Yes | Yes | Yes | Yes (flow) | Manual |
| Backup before replace | Yes (hardlink/copy, atomic swap) | Optional | Optional | Flow-dependent | Manual |
| Crash-safe replace + recovery | Yes | Partial | Partial | Flow-dependent | n/a |
| Hardware detection | Real probe-encode per encoder | Yes | Yes | Yes | Manual |
| ffprobe verification gate | Yes (codec/duration/audio/size) | Health checks | Plugin-dependent | Flow-dependent | No |
| VMAF quality gate | Yes (optional, fail-closed) | Via plugin | Via plugin | Via flow | No |
| HDR10/HLG preserve, DV skip | Yes | Plugin-dependent | Plugin-dependent | Flow-dependent | Manual |
| Persistent queue across restarts | Yes (JSON) | Yes (DB) | Yes (DB) | Yes (DB) | n/a |
| Respects active playback | Yes (ISessionManager) | No (separate process) | No | No | n/a |
| Distributed / multi-node | No | Yes | Remote workers | No | No |
| Plugin / flow system | No | Yes | Yes | Yes | No |
| Web UI | Jellyfin config page (status + history) | Full web UI | Full web UI | Full web UI | Desktop GUI |
| Maturity | 0.0.x, new | Mature | Mature | Mature | Very mature |

## Where this plugin is the better fit

- You already run Jellyfin and don't want to stand up and maintain another container,
  reverse proxy, or worker pool.
- You want the work to respect Jellyfin's own playback sessions (it skips a file that is
  being streamed) and to use the exact jellyfin-ffmpeg the server already ships.
- You want the status and the "what changed, from what to what" history inside the
  Jellyfin dashboard, next to the rest of your admin UI.
- You want a safety-first in-place replace: dry-run by default, verification gates, a
  backup kept until the retention window, and an atomic swap that can't leave the source
  path empty even on a crash.

## Where the mature tools win

- **Scale.** Tdarr distributes encodes across many nodes/GPUs. This plugin processes one
  job at a time on the Jellyfin host.
- **Flexibility.** Tdarr flows and FileFlows let you build arbitrary pipelines (conditional
  logic, file moves, notifications, custom plugins). This plugin does one fixed pipeline.
- **Breadth.** The mature tools have years of edge-case handling across odd containers,
  codecs and subtitle formats. This plugin covers the common cases and deliberately skips
  what it can't guarantee (e.g. Dolby Vision).
- **UI depth.** Full web UIs with per-file controls, charts and history vs. this plugin's
  single config page.

## Honest notes

- "Keeps the Jellyfin item" is not unique to this plugin: Jellyfin keys items by path, so
  *any* tool that replaces a file in place keeps the same item. The real differentiator
  here is "no extra infrastructure" and tight integration, not item-ID magic.
- This is a 0.0.x release operating on your real media. Test it on a small library first.
  The mature tools have a much larger install base and more battle-testing.
