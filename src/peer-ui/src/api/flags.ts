import type { FlagRequest, FlagSummary } from '../types/api';
import { apiFetch } from './client';

export async function submitFlag(request: FlagRequest): Promise<void> {
    const response = await apiFetch('/api/flags', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
    });
    if (!response.ok) {
        throw new Error(`Failed to submit flag: ${response.statusText}`);
    }
}

export async function getManifestFlagSummaries(
    manifestHashes: string[]
): Promise<Record<string, FlagSummary>> {
    try {
        const response = await apiFetch('/api/flags/batch', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ manifestHashes }),
        });
        if (!response.ok) return {};
        return await response.json();
    } catch {
        return {};
    }
}

export async function getManifestFlagSummary(manifestHash: string): Promise<FlagSummary | null> {
    try {
        const response = await apiFetch(`/api/flags/${encodeURIComponent(manifestHash)}/summary`);
        if (!response.ok) return null;
        return await response.json();
    } catch {
        return null;
    }
}

const FLAGGED_STORAGE_KEY = 'mangamesh:flagged';

export function loadLocallyFlagged(): Set<string> {
    try {
        const raw = localStorage.getItem(FLAGGED_STORAGE_KEY);
        return raw ? new Set(JSON.parse(raw) as string[]) : new Set();
    } catch {
        return new Set();
    }
}

export function saveLocallyFlagged(flagged: Set<string>): void {
    localStorage.setItem(FLAGGED_STORAGE_KEY, JSON.stringify(Array.from(flagged)));
}
