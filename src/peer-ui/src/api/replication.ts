import { apiFetch } from './client';

export interface ReplicationChapter {
    manifestHash: string;
    chapterNumber: number;
    title: string;
    language: string;
    scanGroup: string;
    isDownloaded: boolean;
    totalBytes: number;
    localBytes: number;
    totalChunks: number;
    localChunks: number;
    replicaEstimate: number;
    rareChunkCount: number;
    targetReplicas: number;
    minimumReplicas: number;
}

export interface ReplicationSeries {
    seriesId: string;
    seriesTitle: string | null;
    externalMangaId: string | null;
    replicaEstimate: number;
    chapters: ReplicationChapter[];
}

export interface ReplicationOverview {
    series: ReplicationSeries[];
}

export async function getReplicationOverview(): Promise<ReplicationOverview> {
    const res = await apiFetch('/api/replication/overview');
    if (!res.ok) throw new Error(`Failed to fetch replication overview: ${res.status}`);
    return res.json();
}
