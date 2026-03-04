import type { PeerBroadcast } from '../api/broadcasts';

export interface ChapterRelease {
    chapterId: string;
    manifestHash: string;
    nodeIds: string[];
    title: string;
    scanGroup: string;
    language: string;
    quality: string;
    createdUtc: string;
}

export interface AggregatedChapter {
    chapterNumber: number;
    displayTitle: string;
    releases: ChapterRelease[];
}

export interface SeriesEntry {
    seriesId: string;
    seriesTitle: string | null;
    externalMangaId: string | null;
    peerCount: number;
    chapters: AggregatedChapter[];
}

export function groupBySeries(peers: PeerBroadcast[]): SeriesEntry[] {
    const seriesMap = new Map<string, {
        seriesTitle: string | null;
        externalMangaId: string | null;
        peerIds: Set<string>;
        chapterMap: Map<number, Map<string, ChapterRelease>>; // inner key: manifestHash
    }>();

    for (const peer of peers) {
        for (const s of peer.series) {
            if (!seriesMap.has(s.seriesId)) {
                seriesMap.set(s.seriesId, { seriesTitle: s.seriesTitle, externalMangaId: s.externalMangaId ?? null, peerIds: new Set(), chapterMap: new Map() });
            }
            const entry = seriesMap.get(s.seriesId)!;
            if (!entry.seriesTitle && s.seriesTitle) entry.seriesTitle = s.seriesTitle;
            if (!entry.externalMangaId && s.externalMangaId) entry.externalMangaId = s.externalMangaId;
            entry.peerIds.add(peer.nodeId);

            for (const ch of s.chapters) {
                if (!entry.chapterMap.has(ch.chapterNumber)) {
                    entry.chapterMap.set(ch.chapterNumber, new Map());
                }
                const releases = entry.chapterMap.get(ch.chapterNumber)!;
                if (!releases.has(ch.manifestHash)) {
                    releases.set(ch.manifestHash, {
                        chapterId: ch.chapterId,
                        manifestHash: ch.manifestHash,
                        nodeIds: ch.nodeId ? [ch.nodeId] : [],
                        title: ch.title,
                        scanGroup: ch.scanGroup,
                        language: ch.language,
                        quality: ch.quality,
                        createdUtc: ch.createdUtc,
                    });
                } else if (ch.nodeId) {
                    const existing = releases.get(ch.manifestHash)!;
                    if (!existing.nodeIds.includes(ch.nodeId)) existing.nodeIds.push(ch.nodeId);
                }
            }
        }
    }

    return Array.from(seriesMap.entries())
        .map(([seriesId, { seriesTitle, externalMangaId, peerIds, chapterMap }]) => ({
            seriesId,
            seriesTitle,
            externalMangaId,
            peerCount: peerIds.size,
            chapters: Array.from(chapterMap.entries())
                .map(([chapterNumber, releases]) => {
                    const releaseList = Array.from(releases.values())
                        .sort((a, b) => new Date(b.createdUtc).getTime() - new Date(a.createdUtc).getTime());
                    return {
                        chapterNumber,
                        displayTitle: releaseList[0]?.title ?? `Chapter ${chapterNumber}`,
                        releases: releaseList,
                    };
                })
                .sort((a, b) => a.chapterNumber - b.chapterNumber),
        }))
        .sort((a, b) => {
            const latestA = a.chapters.at(-1)?.chapterNumber ?? 0;
            const latestB = b.chapters.at(-1)?.chapterNumber ?? 0;
            return latestB - latestA || a.seriesId.localeCompare(b.seriesId);
        });
}

export function formatDate(iso: string): string {
    if (!iso) return '';
    const d = new Date(iso);
    if (isNaN(d.getTime())) return '';
    return d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}
