import type { StorageStats, StoredManifest, StoredBlob, PagedResult } from '../types/api';
import { apiFetch } from './client';

export async function getStorageStats(): Promise<StorageStats> {
    const response = await apiFetch('/api/node/storage');
    if (!response.ok) throw new Error('Failed to fetch storage stats');
    return await response.json();
}

export async function getStoredManifests(q?: string, offset = 0, limit = 20): Promise<PagedResult<StoredManifest>> {
    const params = new URLSearchParams();
    if (q) params.set('q', q);
    params.set('offset', String(offset));
    params.set('limit', String(limit));
    const response = await apiFetch(`/api/node/storage/manifests?${params}`);
    if (!response.ok) throw new Error('Failed to fetch manifests');
    return await response.json();
}

export async function deleteManifest(hash: string): Promise<void> {
    const response = await apiFetch(`/api/node/storage/manifests/${hash}`, {
        method: 'DELETE'
    });
    if (!response.ok) throw new Error('Failed to delete manifest');
}

export async function bulkDeleteManifests(hashes: string[]): Promise<void> {
    const response = await apiFetch('/api/node/storage/manifests', {
        method: 'DELETE',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(hashes)
    });
    if (!response.ok) throw new Error('Failed to delete manifests');
}

export async function getStoredBlobs(offset = 0, limit = 50): Promise<PagedResult<StoredBlob>> {
    const params = new URLSearchParams();
    params.set('offset', String(offset));
    params.set('limit', String(limit));
    const response = await apiFetch(`/api/node/storage/blobs?${params}`);
    if (!response.ok) throw new Error('Failed to fetch blobs');
    return await response.json();
}

export interface LocalChapterManifest {
    manifestHash: string;
    language: string;
    scanGroup: string;
    quality: string;
    uploadedAt: string;
}

export interface LocalChapter {
    chapterId: string;
    chapterNumber: number;
    volume?: string;
    title: string;
    uploadedAt: string;
    manifests: LocalChapterManifest[];
}

export interface LocalSeriesData {
    seriesId: string;
    seriesTitle?: string;
    externalMangaId?: string;
    chapters: LocalChapter[];
}

export interface LocalSeriesSummary {
    seriesId: string;
    seriesTitle?: string;
    externalMangaId?: string;
    chapterCount: number;
    latestChapter?: number;
    totalSizeBytes: number;
}

export async function getLocalAllSeries(): Promise<LocalSeriesSummary[]> {
    const response = await apiFetch('/api/node/storage/series');
    if (!response.ok) throw new Error(`Failed to fetch local series (HTTP ${response.status})`);
    return await response.json();
}

export async function getLocalSeriesData(seriesId: string): Promise<LocalSeriesData> {
    const response = await apiFetch(`/api/node/storage/series/${encodeURIComponent(seriesId)}`);
    if (!response.ok) throw new Error(`Failed to fetch local series data (HTTP ${response.status})`);
    return await response.json();
}

export async function bulkDeleteBlobs(hashes: string[]): Promise<void> {
    const response = await apiFetch('/api/node/storage/blobs', {
        method: 'DELETE',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(hashes)
    });
    if (!response.ok) throw new Error('Failed to delete blobs');
}
