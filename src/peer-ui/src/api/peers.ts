import type { ManifestSeeder } from '../types/api';
import { apiFetch } from './client';

export async function getManifestSeeders(manifestHash: string): Promise<ManifestSeeder[]> {
    try {
        const response = await apiFetch(`/api/node/peers/manifest/${encodeURIComponent(manifestHash)}`);
        if (!response.ok) return [];
        return await response.json();
    } catch {
        return [];
    }
}
