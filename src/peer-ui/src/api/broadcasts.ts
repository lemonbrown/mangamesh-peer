import { apiFetch } from './client';

export interface BroadcastChapter {
    chapterId: string;
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
    coverUrl: string | null;
    chapters: BroadcastChapter[];
}

export interface PeerBroadcast {
    nodeId: string;
    host: string;
    httpApiPort: number;
    series: BroadcastSeries[];
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
            coverUrl: s.coverUrl ?? s.CoverUrl ?? null,
            chapters: (s.chapters ?? s.Chapters ?? []).map((c: any) => ({
                chapterId: c.chapterId ?? c.ChapterId,
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
