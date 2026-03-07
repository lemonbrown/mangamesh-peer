const DEFAULT_API_KEY = 'mm-peer-default-key-change-me';

// Paths routed to the metadata/index API by the Vite proxy — never send to a peer node.
const METADATA_PATHS = ['/api/mangametadata', '/api/auth', '/api/flags', '/covers'];

export function getApiKey(): string {
    return localStorage.getItem('peerApiKey') ?? DEFAULT_API_KEY;
}

export function getApiBaseUrl(): string {
    const testNodeUrl = localStorage.getItem('testNodeUrl');
    // If running in development with a test harness node selected, use it.
    // Otherwise fallback to empty string (relative to current host for production)
    return testNodeUrl || '';
}

export async function apiFetch(url: string, options: RequestInit = {}): Promise<Response> {
    const headers = new Headers(options.headers);
    headers.set('X-Api-Key', getApiKey());

    // Metadata/index API paths always use relative URLs so the Vite proxy routes them correctly.
    const isPeerPath = !METADATA_PATHS.some(p => url.startsWith(p));
    const fullUrl = url.startsWith('/') ? `${isPeerPath ? getApiBaseUrl() : ''}${url}` : url;

    return fetch(fullUrl, { ...options, headers });
}
