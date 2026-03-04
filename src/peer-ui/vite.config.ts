import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Target URLs can be overridden via environment variables (e.g. in Docker Compose).
// Defaults match the local dev Aspire setup.
const clientApiUrl = process.env.PEER_CLIENT_API_URL ?? 'https://localhost:7124'
const metadataApiUrl = process.env.PEER_METADATA_API_URL ?? 'https://localhost:7030'
const gatewayUrl = process.env.PEER_GATEWAY_URL ?? 'http://localhost:5170'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    host: true,   // listen on 0.0.0.0 so Docker can expose the port
    port: 5173,
    hmr: { host: 'localhost' },  // fix HMR WebSocket on Windows when host: true
    proxy: {
      '^/api/import': {
        target: clientApiUrl,
        changeOrigin: true,
        secure: false
      },
      '^/api/Series': {
        target: clientApiUrl,
        changeOrigin: true,
        secure: false
      },
      '^/api/blob': {
        target: clientApiUrl,
        changeOrigin: true,
        secure: false
      },
      '^/api/file': {
        target: clientApiUrl,
        changeOrigin: true,
        secure: false
      },
      '^/api/mangametadata': {
        target: metadataApiUrl,
        changeOrigin: true,
        secure: false
      },
      '^/api/auth': {
        target: metadataApiUrl,
        changeOrigin: true,
        secure: false,
        rewrite: (path) => path.replace(/^\/api\/auth/, '/api')
      },
      '^/api/keys': {
        target: clientApiUrl,
        changeOrigin: true,
        secure: false
      },
      '^/api/node': {
        target: clientApiUrl,
        changeOrigin: true,
        secure: false
      },
      '^/api/subscriptions': {
        target: clientApiUrl,
        changeOrigin: true,
        secure: false
      },
      '^/api/broadcasts': {
        target: clientApiUrl,
        changeOrigin: true,
        secure: false
      },
      '^/api/peer': {
        target: clientApiUrl,
        changeOrigin: true,
        secure: false
      },
      '^/api/flags': {
        target: metadataApiUrl,
        changeOrigin: true,
        secure: false
      },
      '^/covers': {
        target: metadataApiUrl,
        changeOrigin: true,
        secure: false
      },
      '^/gateway': {
        target: gatewayUrl,
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/gateway/, '')
      }
    }
  }
})
