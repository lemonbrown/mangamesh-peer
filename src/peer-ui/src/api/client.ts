const DEFAULT_API_KEY = 'mm-peer-default-key-change-me';

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
    
    // Auto-prefix URL with base url if it starts with a slash
    const fullUrl = url.startsWith('/') ? `${getApiBaseUrl()}${url}` : url;
    
    return fetch(fullUrl, { ...options, headers });
}
