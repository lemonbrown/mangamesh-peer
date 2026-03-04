import { apiFetch } from './client';

export interface BroadcastChapter {
    chapterId: string;
    manifestHash: string;
    nodeId: string;
    title: string;
    chapterNumber: number;
    language: string;
    scanGroup: string;
    quality: string;
    createdUtc: string;
}

export interface BroadcastSeries {
    seriesId: string;
    seriesTitle: string | null;
    externalMangaId: string | null;
    chapters: BroadcastChapter[];
}

export interface PeerBroadcast {
    nodeId: string;
    host: string;
    httpApiPort: number;
    series: BroadcastSeries[];
}

export async function peekChapter(nodeId: string, manifestHash: string): Promise<string> {
    const params = new URLSearchParams({ nodeId, manifestHash });
    const response = await apiFetch(`/api/broadcasts/peek?${params}`);
    if (!response.ok) {
        const text = await response.text();
        throw new Error(`Peek failed (HTTP ${response.status}): ${text}`);
    }
    const blob = await response.blob();
    return URL.createObjectURL(blob);
}

export async function getBroadcasts(): Promise<PeerBroadcast[]> {
    const response = await apiFetch('/api/broadcasts');
    if (!response.ok) {
        throw new Error(`Failed to fetch broadcasts (HTTP ${response.status})`);
    }
    const data = await response.json();
    return (data as any[]).map((p: any) => ({
        nodeId: p.nodeId ?? p.NodeId,
        host: p.host ?? p.Host,
        httpApiPort: p.httpApiPort ?? p.HttpApiPort,
        series: (p.series ?? p.Series ?? []).map((s: any) => ({
            seriesId: s.seriesId ?? s.SeriesId,
            seriesTitle: s.seriesTitle ?? s.SeriesTitle ?? null,
            externalMangaId: s.externalMangaId ?? s.ExternalMangaId ?? null,
            chapters: (s.chapters ?? s.Chapters ?? []).map((c: any) => ({
                chapterId: c.chapterId ?? c.ChapterId,
                manifestHash: c.manifestHash ?? c.ManifestHash ?? '',
                nodeId: c.nodeId ?? c.NodeId ?? '',
                title: c.title ?? c.Title ?? '',
                chapterNumber: c.chapterNumber ?? c.ChapterNumber ?? 0,
                language: c.language ?? c.Language ?? '',
                scanGroup: c.scanGroup ?? c.ScanGroup ?? '',
                quality: c.quality ?? c.Quality ?? '',
                createdUtc: c.createdUtc ?? c.CreatedUtc ?? '',
            })),
        })),
    }));
}
