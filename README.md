# Jellyfin HLS Downloader

A Jellyfin plugin for downloading HLS streams (`.m3u8`) using `ffmpeg`.

This plugin provides:
- a configuration page in the Jellyfin admin UI,
- a persistent job queue (SQLite),
- a scheduled task for processing jobs,
- REST endpoints to create/list/cancel jobs.

## Requirements

- Jellyfin Server (ABI must match `build.yaml`, currently `10.11.0.0`)
- `ffmpeg` available in the PATH of the Jellyfin process
- Write permissions for the target download path

## Installation (via plugin repository)

1. In Jellyfin: **Dashboard → Plugins → Repositories → Add**
2. Add a repository name, for example `HLS Downloader Repo`
3. Set the repository URL to:

  `https://derh4nnes.github.io/jellyfin-hls-downloader/manifest.json`

4. Save
5. Open **Catalog** and install the **HLS Downloader** plugin

Note:
- The manifest is generated from a **real release asset (`.zip`)** in CI.
- `checksum` is the MD5 hash of the downloaded ZIP contents (not the URL string).

## Plugin Configuration

On the plugin page:
- **Default Download Path**: local target path on the Jellyfin host used when a job does not provide its own output path.

## Usage in Jellyfin UI

On the plugin page you can:
- create new jobs (`HLS URL`, optional `Output Path`),
- refresh the job list,
- start the scheduled task immediately,
- cancel running/queued jobs.

## API

Base route: `/api/hlsdownloader/downloads`

### `POST /api/hlsdownloader/downloads`
Create a new download job.

Request:
```json
{
  "startUrl": "https://example.com/stream.m3u8",
  "outputPath": "/media/downloads"
}
```

`outputPath` is optional.

### `GET /api/hlsdownloader/downloads/jobs`
Return all persisted jobs (newest first).

### `POST /api/hlsdownloader/downloads/jobs/{id}/cancel`
Cancel a queued/running job (the job is deleted).

## Job Behavior

- New jobs start with status `QUEUED`.
- The **HLS Download Job Processor** scheduled task runs every 1 minute by default.
- During processing: `QUEUED -> RUNNING`.
- On success, the job is currently deleted.
- On failure, status is set to `ERROR` and reset to `QUEUED` on plugin startup.

## Local Build

```bash
dotnet build Jellyfin.Plugin.HLSDownloader.sln
```

Or in VS Code via tasks:
- `build`
- `build-and-copy` (publishes and copies to your local Jellyfin plugin directory)

## Release/CI

- `.github/workflows/build.yaml`: builds the plugin
- `.github/workflows/publish.yaml`: publishes release artifacts
- `.github/workflows/pages-manifest.yaml`: generates and deploys `manifest.json` to GitHub Pages

Important for working external installation:
- The release must contain an uploaded `.zip` asset
- Manifest `sourceUrl` must point to that asset
- Manifest `checksum` must be the MD5 of that exact asset

## Development

Project: `Jellyfin.Plugin.HLSDownloader/Jellyfin.Plugin.HLSDownloader.csproj`

Tech stack:
- .NET `net9.0`
- Jellyfin `Jellyfin.Controller` / `Jellyfin.Model`
- EF Core + SQLite for job persistence
