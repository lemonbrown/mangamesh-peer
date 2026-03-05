import { useState, useEffect, useCallback } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import { getBroadcasts, peekChapter } from '../api/broadcasts';
import { groupBySeries, formatDate } from '../utils/broadcastUtils';
import type { SeriesEntry, ChapterRelease } from '../utils/broadcastUtils';
import LangFlag from '../components/LangFlag';

function chFmt(n: number) {
    return Number.isInteger(n) ? String(n) : n.toFixed(1);
}

import { downloadManifest, getLocalSeriesData } from '../api/storage';

function ManifestLink({ seriesId, release }: { seriesId: string; release: ChapterRelease }) {
    // Pick first node as the source node for reading if we need to actively fetch chunks
    const nodeId = release.nodeIds[0] ?? '';
    const to = `/series/${seriesId}/read/${release.chapterId}?manifest=${release.manifestHash}${nodeId ? `&nodeId=${encodeURIComponent(nodeId)}` : ''}`;

    return (
        <Link
            to={to}
            title={release.nodeIds.length > 0 ? `Available on ${release.nodeIds.length} peers` : 'No peers known'}
            className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded border border-blue-200 bg-blue-50 hover:bg-blue-100 hover:border-blue-400 text-blue-700 font-mono text-[11px] transition-colors group"
        >
            <svg className="w-3 h-3 shrink-0 text-blue-400 group-hover:text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
            </svg>
            {release.manifestHash.slice(0, 8)}
        </Link>
    );
}

function DownloadButton({ release, initialDownloaded }: { release: ChapterRelease, initialDownloaded?: boolean }) {
    const [downloading, setDownloading] = useState(false);
    const [downloaded, setDownloaded] = useState(initialDownloaded ?? false);

    useEffect(() => {
        if (initialDownloaded) setDownloaded(true);
    }, [initialDownloaded]);

    const handleDownload = async () => {
        setDownloading(true);
        try {
            await downloadManifest(release.manifestHash);
            setDownloaded(true);
        } catch (e) {
            alert('Failed to download: ' + (e instanceof Error ? e.message : 'Unknown error'));
        } finally {
            setDownloading(false);
        }
    };

    if (downloaded) {
        return (
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded border border-green-200 bg-green-50 text-green-700 text-[11px] font-medium">
                <svg className="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                </svg>
                Downloaded
            </span>
        );
    }

    return (
        <button
            onClick={handleDownload}
            disabled={downloading}
            className="inline-flex items-center gap-1 px-2 py-0.5 rounded border border-gray-200 bg-gray-50 hover:bg-gray-100 text-gray-700 text-[11px] font-medium transition-colors disabled:opacity-50"
        >
            {downloading ? (
                <svg className="w-3 h-3 animate-spin" fill="none" viewBox="0 0 24 24">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                </svg>
            ) : (
                <svg className="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
                </svg>
            )}
            Download
        </button>
    );
}

interface PeekState {
    objectUrl: string;
    manifestHash: string;
}

function PeekModal({ peek, onClose }: { peek: PeekState; onClose: () => void }) {
    useEffect(() => {
        const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
        window.addEventListener('keydown', onKey);
        return () => {
            window.removeEventListener('keydown', onKey);
            URL.revokeObjectURL(peek.objectUrl);
        };
    }, [peek.objectUrl, onClose]);

    return (
        <div
            className="fixed inset-0 z-50 flex items-center justify-center bg-black/70"
            onClick={onClose}
        >
            <div
                className="relative max-w-2xl max-h-[90vh] flex flex-col items-center"
                onClick={e => e.stopPropagation()}
            >
                <div className="flex items-center justify-between w-full mb-2 px-1">
                    <span className="text-white/60 text-xs font-mono">{peek.manifestHash.slice(0, 16)}… (random page peek)</span>
                    <button
                        onClick={onClose}
                        className="text-white/70 hover:text-white text-sm px-2 py-0.5 rounded hover:bg-white/10"
                    >
                        ✕ Close
                    </button>
                </div>
                <img
                    src={peek.objectUrl}
                    alt="Peek page"
                    className="max-h-[80vh] max-w-full object-contain rounded shadow-2xl"
                />
            </div>
        </div>
    );
}

function PeekButton({ release }: { release: ChapterRelease }) {
    const [loading, setLoading] = useState(false);
    const [peek, setPeek] = useState<PeekState | null>(null);
    const [error, setError] = useState<string | null>(null);

    const nodeId = release.nodeIds[0];
    if (!nodeId) return null;

    const handlePeek = async () => {
        setLoading(true);
        setError(null);
        try {
            const objectUrl = await peekChapter(nodeId, release.manifestHash);
            setPeek({ objectUrl, manifestHash: release.manifestHash });
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Peek failed');
        } finally {
            setLoading(false);
        }
    };

    return (
        <>
            <button
                onClick={handlePeek}
                disabled={loading}
                className="inline-flex items-center gap-1 px-2 py-0.5 rounded border border-gray-200 bg-gray-50 hover:bg-gray-100 text-gray-500 hover:text-gray-700 text-[11px] transition-colors disabled:opacity-50"
            >
                {loading ? (
                    <svg className="w-3 h-3 animate-spin" fill="none" viewBox="0 0 24 24">
                        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                    </svg>
                ) : (
                    <svg className="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                    </svg>
                )}
                Peek
            </button>
            {error && <span className="text-[10px] text-red-500">{error}</span>}
            {peek && <PeekModal peek={peek} onClose={() => setPeek(null)} />}
        </>
    );
}

export default function BroadcastSeriesDetail() {
    const { seriesId } = useParams<{ seriesId: string }>();
    const location = useLocation();

    const [entry, setEntry] = useState<SeriesEntry | null>(location.state as SeriesEntry | null);
    const [localManifests, setLocalManifests] = useState<Set<string>>(new Set());
    const [loading, setLoading] = useState(!entry);
    const [error, setError] = useState<string | null>(null);

    const loadLocalManifests = useCallback(async (id: string) => {
        try {
            // We need all manifests, not just explicitly downloaded ones, because 
            // the network may have replicated it which still counts as a blob we have locally.
            // Oh wait, getLocalSeriesData signature: getLocalSeriesData(seriesId: string, includeReplicated: boolean = false)
            const localData = await getLocalSeriesData(id, true);
            const hashes = new Set<string>();
            for (const ch of localData.chapters) {
                for (const m of ch.manifests) {
                    if (m.isDownloaded) hashes.add(m.manifestHash);
                }
            }
            setLocalManifests(hashes);
        } catch (e) {
            console.warn("Could not load local library stats", e);
        }
    }, []);

    const load = useCallback(async () => {
        setLoading(true);
        setError(null);
        try {
            const all = groupBySeries(await getBroadcasts());
            const found = all.find(s => s.seriesId === seriesId) ?? null;
            setEntry(found);

            if (found) {
                await loadLocalManifests(found.seriesId);
            }
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Failed to fetch broadcasts.');
        } finally {
            setLoading(false);
        }
    }, [seriesId, loadLocalManifests]);

    useEffect(() => {
        if (!entry) {
            load();
        } else {
            // If entry was provided via location.state, we still need to fetch the local manifest sync status
            loadLocalManifests(entry.seriesId);
        }
    }, [entry, load, loadLocalManifests]);

    if (loading) return <div className="p-8 text-gray-500">Loading...</div>;
    if (error) return <div className="p-8 text-red-500">{error}</div>;

    const title = entry?.seriesTitle ?? entry?.seriesId ?? seriesId;

    return (
        <div className="space-y-6">
            {/* Series header */}
            <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
                <div className="flex gap-5 p-5">
                    <div className="shrink-0 w-28 rounded overflow-hidden bg-gray-100 self-start relative flex items-center justify-center" style={{ minHeight: '10rem' }}>
                        <svg className="w-8 h-8 text-gray-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253" />
                        </svg>
                        {entry?.externalMangaId && (
                            <img
                                src={`/covers/${entry.externalMangaId}.card.webp`}
                                alt={title ?? ''}
                                className="absolute inset-0 w-full h-full object-cover"
                                onError={(e) => { (e.target as HTMLImageElement).style.display = 'none'; }}
                            />
                        )}
                    </div>

                    <div className="flex-1 min-w-0">
                        <h1 className="text-2xl font-bold text-gray-900 leading-tight">{title}</h1>
                        <div className="flex flex-wrap gap-3 mt-3 text-sm text-gray-500">
                            {entry && <span><span className="font-semibold text-gray-700">{entry.chapters.length}</span> chapters</span>}
                            {entry && <span><span className="font-semibold text-green-600">{entry.peerCount}</span> {entry.peerCount === 1 ? 'peer' : 'peers'}</span>}
                        </div>
                        <div className="flex items-center gap-3 mt-4">
                            <button
                                onClick={load}
                                disabled={loading}
                                className="px-4 py-2 rounded-md text-sm font-medium bg-gray-100 text-gray-700 hover:bg-gray-200 transition-colors disabled:opacity-50"
                            >
                                Refresh
                            </button>
                            <Link to="/broadcasts" className="text-blue-600 hover:underline text-sm">
                                ← Broadcasts
                            </Link>
                        </div>
                    </div>
                </div>
            </div>

            // Chapter list
            {!entry || entry.chapters.length === 0 ? (
                <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-8 text-center text-gray-500">
                    {!entry ? 'Series not found in current DHT broadcasts.' : 'No chapters found.'}
                </div>
            ) : (
                <div className="bg-white rounded-lg shadow-sm border border-gray-200 divide-y divide-gray-100">
                    {[...entry.chapters].reverse().map((ch) => (
                        <div key={ch.chapterNumber} className="p-4">
                            {/* Chapter heading */}
                            <div className="flex items-baseline gap-2 mb-3">
                                <span className="text-sm font-bold text-gray-800">
                                    Ch. {chFmt(ch.chapterNumber)}
                                </span>
                                {ch.displayTitle && ch.displayTitle !== `Chapter ${ch.chapterNumber}` && (
                                    <span className="text-sm text-gray-500 truncate">{ch.displayTitle}</span>
                                )}
                            </div>

                            {/* Releases */}
                            <div className="space-y-3 pl-3 border-l-2 border-gray-100">
                                {ch.releases.map((r) => (
                                    <div key={r.manifestHash} className="flex flex-wrap items-center gap-2">
                                        <span className="bg-blue-100 text-blue-700 px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider flex items-center gap-1">
                                            <LangFlag code={r.language} /> {r.language}
                                        </span>
                                        {r.quality && r.quality !== 'Unknown' && (
                                            <span className="bg-gray-100 text-gray-600 px-2 py-0.5 rounded text-[10px] font-medium uppercase tracking-wider">
                                                {r.quality}
                                            </span>
                                        )}
                                        {r.scanGroup && (
                                            <span className="text-sm font-medium text-gray-700">{r.scanGroup}</span>
                                        )}
                                        {r.createdUtc && (
                                            <span className="text-[11px] text-gray-400">{formatDate(r.createdUtc)}</span>
                                        )}
                                        {r.nodeIds.length > 0 && <PeekButton release={r} />}
                                        <ManifestLink seriesId={seriesId!} release={r} />
                                        <DownloadButton release={r} initialDownloaded={localManifests.has(r.manifestHash)} />
                                        {r.nodeIds.length === 0 && (
                                            <span className="text-[11px] text-gray-400 italic">(No nodes currently actively seeding)</span>
                                        )}
                                    </div>
                                ))}
                            </div>
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
}
