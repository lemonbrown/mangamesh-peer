# MangaMesh Peer

The **MangaMesh Peer** is your personal gateway to the decentralized MangaMesh network. By running a peer, you become an active participant in a resilient, community-driven library of manga.

Unlike traditional platforms that rely on central servers, MangaMesh peers connect directly to each other to share content.

## What Can You Do?

- **Browse & Read:** Search through a vast, community-maintained catalog of series and read chapters directly in your browser. As you read, your peer securely fetches pieces of the chapter from other nodes in the network.
- **Support the Network:** Whenever you read a chapter or download it, your peer automatically helps store and distribute that content to others. By simply leaving your peer running, you help make the network faster and more resilient.
- **Publish Your Own Content:** Have manga chapters stored locally on your computer? You can easily import local files or ZIP archives into your peer. Once imported, your peer will announce the new content to the network, making it instantly discoverable and available for anyone else to read.
- **Manage Subscriptions:** Follow your favorite series and let your peer keep track of updates.

## The Desktop UI

The simplest way to interact with your peer is through the **Peer UI**, a built-in web dashboard that provides a polished, desktop-like experience.

Through the dashboard, you can:
- **View your node's live status**, including how many other peers you are connected to.
- **Check exactly how much storage** your peer is contributing to the network.
- **Manage your cryptographic identity** (your node's unique keypair).
- **Monitor real-time logs** of network activity.

## Getting Started

*(If you are a developer looking to run the node locally or via Docker, please refer to the technical guides below.)*

For most users, simply launching the Peer UI is all you need to do:
1. Open the UI in your browser.
2. Set up your storage limits (e.g., allowing up to 5 GB of local storage for seeding).
3. Start browsing the network!

---

## Technical Overview for Developers

Under the hood, the MangaMesh Peer consists of several integrated components:

- **Core DHT Node:** Operates headlessly using the Kademlia protocol to route requests and store content directly over TCP. Content is split into chunks, content-addressed, and cryptographically signed.
- **REST API:** A local HTTP API that wraps the core node, providing simple endpoints for the UI to trigger imports, fetch blobs, and manage settings.
- **React SPA:** The frontend dashboard (built with Vite and Tailwind) that communicates with your local REST API.

### Running Locally

```bash
# Run the backend API
cd MangaMesh.Peer.ClientApi
dotnet run

# Run the UI
cd mangamesh-peer-ui
npm install
npm run dev
```

For full API documentation, start the REST API and navigate to `http://localhost:8080/swagger`.
