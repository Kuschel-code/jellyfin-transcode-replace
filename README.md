# Transcode & Replace

A Jellyfin plugin that re-encodes the files in your libraries to a smaller codec
(HEVC by default, AV1 or H.264 optional) and replaces the originals in place. It
detects which encoders actually work on your machine, runs the jobs from a queue
that survives restarts, checks every output before it touches anything, and keeps a
backup of the original until the new file is verified.

It is meant for the case where you have a pile of large H.264 files and want them as
HEVC without re-importing everything or breaking your existing library entries. The
path stays the same, so the Jellyfin item ID stays the same.

## A warning before you turn it on

This plugin overwrites your media files. That is the whole point, and it is also how
you lose data if something is wrong. Read this:

- It starts in dry-run. Nothing is written until you turn that off. The first run
  only logs what it would do.
- Before replacing anything it re-probes the new file with ffprobe and checks the
  codec, the duration (must be within 0.5%), the audio track count and the file size.
  If any check fails the original is left alone and the temporary file is deleted.
- With the default replace policy the original is moved to a `.trbak` file first, the
  new file is moved into place, and the backup is only deleted after the retention
  window (7 days by default). If the move fails, the backup is rolled back.
- The temporary file is written in the same folder as the source, so the final move
  is a rename on one filesystem and is atomic. It never leaves a half-written file at
  the real path.
- Dolby Vision is skipped by default because it cannot be preserved through a generic
  re-encode. HDR10 and HLG are kept (10-bit, BT.2020 metadata passed through).
- A file that is currently being streamed is skipped until the stream ends.

If you are not sure, leave dry-run on, look at the log, and only then switch it off.

## Requirements

- Jellyfin 10.11 (the plugin targets ABI 10.11.0.0 and .NET 9).
- The bundled jellyfin-ffmpeg. The plugin uses its ffmpeg/ffprobe, so you don't
  configure a path.

## How it works

There are two parts. A scheduled task ("Transcode & Replace: Discover media") walks
the libraries and adds files to a queue. A background worker takes one job at a time
and runs it through the pipeline: probe, pick an encoder, encode to a temp file,
verify, optionally score with VMAF, then replace.

Encoder detection does not trust ffmpeg's compiled-in list. For each candidate
(NVENC, QSV, VAAPI, AMF, VideoToolbox, software) it runs a one-second test encode and
keeps only the ones that exit cleanly. The result is cached.

The queue is a single JSON file in the plugin data folder. No database, so there are
no extra native libraries to ship. Writes are atomic. On startup, any job that was
mid-flight when the server stopped is reset to pending and its leftover temp files are
removed.

## Configuration

Dashboard, then Plugins, then Transcode & Replace.

- Dry-run: on by default. Off means it will actually replace files.
- Target codec: HEVC, AV1 or H.264.
- Quality (CRF/CQ): 23 is a reasonable HEVC default. Lower means better and larger.
- Quality vs. speed: prefer software (libx265, best quality per bit) or hardware (fast).
- Encoder preference: auto, force software, prefer hardware, or a specific encoder name.
- Max parallel jobs: 1 is recommended, especially for GPU encoders.
- Audio: copy, copy plus an added AAC track, or transcode to AAC.
- Container: keep the source container, or force mkv/mp4. mp4 falls back to mkv when
  the source has image-based subtitles (PGS/VobSub), which mp4 can't hold.
- Minimum source bitrate: skip files below this, so you only touch the big ones.
- Skip if already target codec: on, so re-running is safe.
- Replace policy: backup-then-delete (default), keep side by side, or hard replace.
- Backup retention: days to keep the `.trbak` backup.
- VMAF gate: off by default. When on, the output is scored against the source and
  must be at least the threshold (95) or the job fails and the original is kept.
- Skip HDR / Skip Dolby Vision: Dolby Vision is skipped by default.
- Run only when idle: pause while anything is playing.

## Build

Needs the .NET 9 SDK.

```
dotnet build -c Release
dotnet test  -c Release
```

The DLL ends up in
`Jellyfin.Plugin.TranscodeReplace/bin/Release/net9.0/Jellyfin.Plugin.TranscodeReplace.dll`.

## Install

Either grab the zip from a release or build it yourself. The zip contains the DLL and
a `meta.json`. Put both in a folder under your Jellyfin `plugins` directory, for
example `plugins/Transcode & Replace/`, then restart Jellyfin. Configure it and leave
dry-run on for the first run.

## Status

Everything described above is implemented: hardware probe, persistent queue and
discovery, the ffmpeg argument builder, ffprobe verification, the VMAF gate, HDR and
Dolby Vision handling, the playback/idle/disk guards, atomic replace with backup and
permission preservation, and a status endpoint for the config page. There are 53 unit
tests covering the argument builder, the parsers, the verifier and the queue.

This is a 0.0.1 release. It works, but it is replacing your files, so test it on a
small library first.

## License

MIT. See [LICENSE](LICENSE).
