const DEFAULT_API_KEY = 'mm-peer-default-key-change-me';

export function getApiKey(): string {
    return localStorage.getItem('peerApiKey') ?? DEFAULT_API_KEY;
}

export async function apiFetch(url: string, options: RequestInit = {}): Promise<Response> {
    const headers = new Headers(options.headers);
    headers.set('X-Api-Key', getApiKey());
    return fetch(url, { ...options, headers });
}
