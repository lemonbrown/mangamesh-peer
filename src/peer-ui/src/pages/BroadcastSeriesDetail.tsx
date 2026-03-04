import { useState, useEffect, useCallback } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import { getBroadcasts } from '../api/broadcasts';
import { groupBySeries, formatDate } from '../utils/broadcastUtils';
import type { SeriesEntry } from '../utils/broadcastUtils';
import LangFlag from '../components/LangFlag';

function chFmt(n: number) {
    return Number.isInteger(n) ? String(n) : n.toFixed(1);
}

export default function BroadcastSeriesDetail() {
    const { seriesId } = useParams<{ seriesId: string }>();
    const location = useLocation();

    const [entry, setEntry] = useState<SeriesEntry | null>(location.state as SeriesEntry | null);
    const [expandedRelease, setExpandedRelease] = useState<string | null>(null);
    const [loading, setLoading] = useState(!entry);
    const [error, setError] = useState<string | null>(null);

    const load = useCallback(async () => {
        setLoading(true);
        setError(null);
        try {
            const all = groupBySeries(await getBroadcasts());
            const found = all.find(s => s.seriesId === seriesId) ?? null;
            setEntry(found);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Failed to fetch broadcasts.');
        } finally {
            setLoading(false);
        }
    }, [seriesId]);

    useEffect(() => {
        if (!entry) load();
    }, [entry, load]);

    if (loading) return <div className="p-8 text-gray-500">Loading...</div>;
    if (error) return <div className="p-8 text-red-500">{error}</div>;

    const title = entry?.seriesTitle ?? entry?.seriesId ?? seriesId;

    return (
        <div className="space-y-6">
            {/* Series header */}
            <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
                <div className="flex gap-5 p-5">
                    {/* Cover art */}
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

                    {/* Info */}
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

            {/* Chapters */}
            <div className="space-y-4">
                {!entry || entry.chapters.length === 0 ? (
                    <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-8 text-center text-gray-500">
                        {!entry ? 'Series not found in current DHT broadcasts.' : 'No chapters found.'}
                    </div>
                ) : (
                    entry.chapters.map((ch) => (
                        <div key={ch.chapterNumber} className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
                            <div className="px-4 py-2 bg-gray-50 border-b border-gray-100 flex items-center justify-between">
                                <h3 className="text-sm font-bold text-gray-700">
                                    Chapter {chFmt(ch.chapterNumber)}
                                    {ch.displayTitle && ch.displayTitle !== `Chapter ${ch.chapterNumber}` && (
                                        <span className="ml-2 font-normal text-gray-500">- {ch.displayTitle}</span>
                                    )}
                                </h3>
                            </div>

                            <div className="divide-y divide-gray-50">
                                {ch.releases.map((r) => (
                                    <div key={r.chapterId}>
                                        <div
                                            className="p-4 flex justify-between items-center cursor-pointer hover:bg-gray-50 transition-colors"
                                            onClick={() => setExpandedRelease(expandedRelease === r.chapterId ? null : r.chapterId)}
                                        >
                                            <div className="flex-1 min-w-0">
                                                <div className="flex items-center gap-3">
                                                    <span className="bg-blue-100 text-blue-700 px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider flex items-center gap-1">
                                                        <LangFlag code={r.language} /> {r.language}
                                                    </span>
                                                    {r.quality && r.quality !== 'Unknown' && (
                                                        <span className="bg-gray-100 text-gray-600 px-2 py-0.5 rounded text-[10px] font-medium uppercase tracking-wider">
                                                            {r.quality}
                                                        </span>
                                                    )}
                                                    {r.scanGroup && (
                                                        <span className="text-sm font-medium text-gray-700 truncate max-w-[200px]">
                                                            {r.scanGroup}
                                                        </span>
                                                    )}
                                                </div>
                                                <div className="flex items-center gap-3 text-[11px] text-gray-400 mt-1 font-mono truncate">
                                                    {r.createdUtc && <span className="font-sans text-gray-500 shrink-0">{formatDate(r.createdUtc)}</span>}
                                                    {r.manifestHash && <span className="shrink-0" title={r.manifestHash}>manifest: {r.manifestHash.slice(0, 12)}…</span>}
                                                </div>
                                            </div>
                                            <svg className={`w-4 h-4 text-gray-400 shrink-0 transition-transform ${expandedRelease === r.chapterId ? 'rotate-180' : ''}`} fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                                            </svg>
                                        </div>
                                        {expandedRelease === r.chapterId && (
                                            <div className="px-4 pb-3 bg-gray-50 border-t border-gray-100">
                                                <p className="text-[11px] font-semibold text-gray-500 uppercase tracking-wider mb-1.5">Broadcasting nodes</p>
                                                {(r.nodeIds ?? []).length === 0 ? (
                                                    <p className="text-xs text-gray-400">No nodes known.</p>
                                                ) : (
                                                    <ul className="space-y-0.5">
                                                        {(r.nodeIds ?? []).map(nid => (
                                                            <li key={nid} className="font-mono text-[11px] text-gray-600" title={nid}>{nid}</li>
                                                        ))}
                                                    </ul>
                                                )}
                                            </div>
                                        )}
                                    </div>
                                ))}
                            </div>
                        </div>
                    ))
                )}
            </div>
        </div>
    );
}
