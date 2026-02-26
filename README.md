# MangaMesh.Peer

The peer node implementation for the MangaMesh decentralized content distribution network. A peer stores, serves, and retrieves content over a Kademlia DHT using cryptographically signed content manifests.

---

## Projects

| Project | Type | Role |
|---|---|---|
| **MangaMesh.Peer.Core** | Console app | Standalone DHT node — runs headless, serves content over TCP |
| **MangaMesh.Peer.ClientApi** | ASP.NET Core | REST API server — full peer node with HTTP endpoints for chapter management |
| **mangamesh-peer-ui** | React SPA | Desktop client UI — browse, read, and publish content via the local peer node |

`ClientApi` builds on `Core` and adds the HTTP layer on top of the same DHT, storage, and import stack. The UI connects to `ClientApi` (and the Index API for metadata) over HTTP.

---

## How It Works

A peer joins the network by bootstrapping into the DHT from a list of known nodes. It can import local content, which are split into content-addressed chunks, signed with an Ed25519 key, and announced to the tracker. Other peers discover chapters via the DHT or tracker and fetch content directly over TCP.

**Import flow:**

```
Local files (directory or ZIP)
  → split into chunks, stored by SHA-256 hash
  → PageManifest + ChapterManifest built and signed
  → announced to tracker and DHT
  → discoverable and fetchable by any peer
```

**Fetch flow:**

```
ManifestHash (from tracker or DHT lookup)
  → fetch ChapterManifest from peer
  → fetch each PageManifest
  → fetch and reassemble blob chunks
  → display image
```

---

## Project Structure

```
MangaMesh.Peer/
├── MangaMesh.Peer.Core/        Core DHT node (runs standalone or embedded)
│   ├── Blob/                   Content-addressed chunk storage
│   ├── Chapters/               Chapter import and publishing pipeline
│   ├── Keys/                   Ed25519 identity and signing
│   ├── Manifests/              Manifest storage (SQLite)
│   ├── Node/                   DHT node, routing table, bootstrap
│   ├── Tracker/                Tracker client (peer/manifest registration)
│   ├── Transport/              TCP transport and protocol routing
│   ├── config/bootstrap_nodes.yml
│   └── Program.cs
│
├── MangaMesh.Peer.ClientApi/   REST API wrapper around Core
│   ├── Controllers/            Import, blob, manifest, node, keys, series, storage, subscriptions
│   ├── Services/               Import orchestration, challenge/auth
│   └── Program.cs
│
└── mangamesh-peer-ui/          React SPA (Vite / Tailwind / Nginx)
    ├── src/
    │   ├── api/                HTTP client layer
    │   ├── pages/              Route-level views
    │   └── components/         Shared UI components
    └── Dockerfile
```

---

## Configuration

Backend options are set in `appsettings.json` or via environment variables (use `__` as the section separator, e.g. `Dht__Port`).

| Section | Key | Default | Description |
|---|---|---|---|
| BlobStore | `RootPath` | `input` | Directory for stored content chunks |
| BlobStore | `MaxStorageBytes` | `5368709120` | Storage cap (5 GB) |
| ManifestStore | `RootPath` | `input/manifests` | Directory for manifest files |
| Dht | `Port` | `3001` | TCP listen port |
| Dht | `BootstrapNodesPath` | `config/bootstrap_nodes.yml` | Bootstrap node list |
| Dht | `BootstrapNodes` | _(empty)_ | Inline bootstrap addresses (overrides YAML) |
| — | `TrackerUrl` | — | Index tracker base URL (e.g. `http://localhost:7030`) |
| — | `Database:Path` | `data/mangamesh.db` | SQLite database path |

---

## Running

### Local development

```bash
# REST API + full peer node (recommended)
cd MangaMesh.Peer.ClientApi
dotnet run
# API at http://localhost:8080, Swagger at /swagger

# Headless peer node only
cd MangaMesh.Peer.Core
dotnet run
```

```bash
# UI dev server
cd mangamesh-peer-ui
npm install
npm run dev
# http://localhost:5173
```

The UI expects the peer API at `https://localhost:7124` and the Index API at `https://localhost:7030` by default. These can be changed in `vite.config.ts`.

### Docker

From `src/`:

```bash
docker compose up peer-master peer.ui-master
```

| Service | Port | Notes |
|---|---|---|
| `peer-master` | 8080 (API), 4200 (DHT) | Primary peer node |
| `peer.ui-master` | 7124 | UI for `peer-master` |
| `peer-slave` | 8081 (API), 3000 (DHT) | Second peer, bootstraps via `peer-master` |
| `peer.ui-slave` | 7125 | UI for `peer-slave` |
| `index.api` | 7030 | Tracker (required for registration) |

---

## REST API

All endpoints are under `/api`. Swagger UI is available at `/swagger`.

| Area | Endpoints | Purpose |
|---|---|---|
| Import | `POST /import/chapter`, `POST /import/upload` | Import chapters from local path or ZIP upload |
| Blobs | `GET /blob/{hash}`, `GET /file/{pageHash}` | Fetch content chunks or a reassembled page |
| Manifests | `GET/POST /manifest` | Read and store chapter manifests |
| Node | `GET /node/status` | Node ID, peer count, DHT state |
| Keys | `/keys/*` | Generate keypair, challenge-response auth |
| Series | `/series/*` | Browse and search series from the tracker |
| Storage | `GET /storage` | Disk usage breakdown |
| Subscriptions | `/subscriptions/*` | Manage series subscriptions |
| Logs | `GET /logs` | Recent node activity logs |

Upload limit: **500 MB**.

---

## Peer UI

The React SPA provides a full desktop client for operating a peer node.

| Route | Purpose |
|---|---|
| `/` | Dashboard — node status, peer count, storage, subscription updates |
| `/series` | Browse and search series |
| `/series/:id` | Series detail and chapter list |
| `/series/:id/read/:chapterId` | Content reader |
| `/import` | Publish chapters with key signing |
| `/subscriptions` | Manage subscriptions |
| `/storage` | Storage usage |
| `/keys` | Manage node keypair |
| `/logs` | Node activity logs |

In Docker, Nginx serves the built assets and proxies API calls. The three backend URLs are injected at container start via environment variables:

| Variable | Default | Target |
|---|---|---|
| `PEER_CLIENT_API_URL` | `http://peer-master:8080` | Peer ClientApi |
| `PEER_METADATA_API_URL` | `http://index.api:7030` | Index API (metadata, covers) |
| `PEER_GATEWAY_URL` | `http://gateway:5170` | Gateway (optional) |

---

## Testing

```bash
# Unit tests
dotnet test tests/MangaMesh.Peer.Tests/

# Integration tests
dotnet test tests/MangaMesh.IntegrationTests/MangaMesh.IntegrationTests.csproj
```

Unit tests (MSTest + Moq) cover DHT routing, content protocol, key management, and chapter import. Integration tests spin up in-process nodes with no external dependencies (one E2E test requires a live tracker and is expected to fail without it).

---

## Dependencies

| Package | Purpose |
|---|---|
| `Microsoft.Extensions.Hosting` | Generic host, DI, configuration |
| `Microsoft.EntityFrameworkCore.Sqlite` | SQLite storage for keys and manifests |
| `NSec.Cryptography` | Ed25519 signing and key generation |
| `YamlDotNet` | Bootstrap node YAML parsing |
| `Swashbuckle.AspNetCore` | Swagger UI (ClientApi only) |
