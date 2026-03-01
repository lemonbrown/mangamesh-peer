# MangaMesh Peer

The peer node implementation for the MangaMesh network. A peer is responsible for storing, sharing, and retrieving manga content across the decentralized network.

## Functionality

A MangaMesh peer acts as both a client and a server on the network, handling the following core functions:

*   **Content Retrieval**: Locating and downloading manga chapters from other peers on the network.
*   **Content Seeding**: Storing downloaded chapters locally up to configurable limits, and automatically serving those files to other peers who request them.
*   **Content Publishing**: Importing local manga image files or ZIP archives, splitting them into shareable uniform chunks, and announcing their availability to the broader network.
*   **Node Management**: Maintaining the node's cryptographic identity, tracking series subscriptions, and monitoring network connection states.

## Components

The peer is composed of three integrated projects:

| Component | Role | Description |
|---|---|---|
| **MangaMesh.Peer.Core** | Network Layer | A standalone node that connects to the DHT, handles peer routing, and manages direct peer-to-peer data transfer over TCP. |
| **MangaMesh.Peer.ClientApi** | Application Layer | An ASP.NET Core REST API that wraps the core node, exposing HTTP endpoints for chapter management, imports, and node configuration. |
| **mangamesh-peer-ui** | User Interface | A React SPA providing a desktop-like web client for users to browse the network catalog, read chapters, and manage their node. |

## Running Locally

For standard usage, you need to run both the API backend and the frontend UI.

**1. Start the Backend API**
```bash
cd MangaMesh.Peer.ClientApi
dotnet run
# The API will be available at http://localhost:8080
# API documentation (Swagger) is available at http://localhost:8080/swagger
```

**2. Start the Frontend UI**
```bash
cd mangamesh-peer-ui
npm install
npm run dev
# The UI dev server will be available at http://localhost:5173
```

By default, the UI expects your local peer API at `https://localhost:7124` (or `http://localhost:8080`) and the central MangaMesh Index API at `https://localhost:7030`.

## Docker Configuration

The entire peer stack can be started using the central Docker Compose configuration from the `src/` directory:

```bash
docker compose up peer-master peer.ui-master
```

| Service | Exposes | Description |
|---|---|---|
| `peer-master` | 8080 (REST API), 4200 (TCP DHT) | The backend peer node |
| `peer.ui-master` | 7124 (HTTP) | The frontend UI |

## Configuration

Backend behavior is configured via `appsettings.json` in the `ClientApi` project or through environment variables. 

Key storage locations:
*   **BlobStore**: The directory where content chunks are stored (`input` by default). The storage cap defaults to 5 GB.
*   **ManifestStore**: The directory for chapter manifests (`input/manifests` by default).
*   **Database Path**: The SQLite database tracking local node state and identities (`data/mangamesh.db` by default).

*Note: Content imports via the API are currently capped at a file size limit of 500 MB.*

## Testing

```bash
# Unit tests
dotnet test tests/MangaMesh.Peer.Tests/

# Integration tests
dotnet test tests/MangaMesh.IntegrationTests/MangaMesh.IntegrationTests.csproj
```
Integration tests spin up in-process nodes to verify system functionality (routing, transferring files, importing data) without external network dependencies.
