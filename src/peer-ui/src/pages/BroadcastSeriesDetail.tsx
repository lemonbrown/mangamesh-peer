import { useState, useEffect, useCallback } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import { getBroadcasts } from '../api/broadcasts';
import { groupBySeries, formatDate } from '../utils/broadcastUtils';
import type { SeriesEntry } from '../utils/broadcastUtils';
import { langCountryCode } from '../utils/language';

function LangFlag({ code }: { code: string }) {
    const country = langCountryCode(code);
    if (!country) return <span className="text-xs text-gray-400">{code}</span>;
    return <span className={`fi fi-${country} rounded-sm text-base`} title={code} />;
}

function chFmt(n: number) {
    return Number.isInteger(n) ? String(n) : n.toFixed(1);
}

export default function BroadcastSeriesDetail() {
    const { seriesId } = useParams<{ seriesId: string }>();
    const location = useLocation();

    const [entry, setEntry] = useState<SeriesEntry | null>(location.state as SeriesEntry | null);
    const [loading, setLoading] = useState(!entry);
    const [error, setError] = useState<string | null>(null);
    const [expanded, setExpanded] = useState<Set<number>>(new Set());

    const load = useCallback(async () => {
        setLoading(true);
        setError(null);
        try {
            const all = groupBySeries(await getBroadcasts());
            const found = all.find(s => s.seriesId === seriesId) ?? null;
            setEntry(found);
            setExpanded(new Set());
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Failed to fetch broadcasts.');
        } finally {
            setLoading(false);
        }
    }, [seriesId]);

    useEffect(() => {
        if (!entry) load();
    }, [entry, load]);

    function toggle(chapterNumber: number) {
        setExpanded(prev => {
            const next = new Set(prev);
            next.has(chapterNumber) ? next.delete(chapterNumber) : next.add(chapterNumber);
            return next;
        });
    }

    const title = entry?.seriesTitle ?? entry?.seriesId ?? seriesId;

    return (
        <div className="space-y-6 pb-12">
            {/* Header */}
            <div className="flex items-start justify-between gap-4">
                <div className="flex items-start gap-4 min-w-0">
                    {entry?.coverUrl && (
                        <img
                            src={entry.coverUrl}
                            alt=""
                            className="w-20 h-28 object-cover rounded-lg shadow-sm shrink-0 bg-gray-100"
                        />
                    )}
                    <div className="min-w-0">
                        <Link to="/broadcasts" className="text-xs text-gray-400 hover:text-blue-500 transition-colors mb-1 inline-block">
                            ← Broadcasts
                        </Link>
                        <h1 className="text-3xl font-bold text-gray-900 truncate">{title}</h1>
                        {entry && (
                            <p className="text-gray-500 text-sm mt-0.5">
                                {entry.chapters.length} {entry.chapters.length === 1 ? 'chapter' : 'chapters'} &middot; {entry.peerCount} {entry.peerCount === 1 ? 'peer' : 'peers'}
                            </p>
                        )}
                    </div>
                </div>
                <button
                    onClick={load}
                    disabled={loading}
                    className="flex items-center gap-1.5 px-3 py-2 rounded-md text-sm font-medium text-gray-600 hover:bg-gray-100 transition-colors disabled:opacity-50 shrink-0"
                >
                    <svg className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                    </svg>
                    Refresh
                </button>
            </div>

            {error && (
                <div className="text-red-500 text-sm bg-red-50 border border-red-200 rounded-lg px-4 py-3">{error}</div>
            )}

            {loading ? (
                <div className="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden">
                    {[1, 2, 3, 4, 5, 6].map(i => (
                        <div key={i} className="px-5 py-3 border-b border-gray-50 animate-pulse flex items-center gap-3">
                            <div className="h-3 bg-gray-200 rounded w-10" />
                            <div className="h-3 bg-gray-100 rounded flex-1" />
                        </div>
                    ))}
                </div>
            ) : !entry ? (
                <div className="text-gray-500 italic p-12 text-center bg-gray-50 rounded-xl border border-dashed border-gray-200">
                    Series not found in current DHT broadcasts.
                </div>
            ) : (
                <div className="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden">
                    <div className="divide-y divide-gray-50">
                        {entry.chapters.map(ch => {
                            const isOpen = expanded.has(ch.chapterNumber);
                            const uniqueLangs = [...new Set(ch.releases.map(r => r.language).filter(Boolean))];

                            return (
                                <div key={ch.chapterNumber}>
                                    <button
                                        onClick={() => toggle(ch.chapterNumber)}
                                        className="w-full px-5 py-3 flex items-center gap-3 hover:bg-gray-50 transition-colors text-left"
                                    >
                                        <span className="text-xs font-mono font-semibold text-blue-500 w-10 shrink-0">
                                            {chFmt(ch.chapterNumber)}
                                        </span>
                                        <span className="text-sm text-gray-800 truncate flex-1">{ch.displayTitle}</span>
                                        <span className="flex items-center gap-1.5 shrink-0">
                                            {uniqueLangs.map(lang => <LangFlag key={lang} code={lang} />)}
                                            {ch.releases.length > 1 && (
                                                <span className="text-[10px] font-medium text-gray-500 bg-gray-100 px-1.5 py-0.5 rounded-full">
                                                    {ch.releases.length}
                                                </span>
                                            )}
                                            <svg
                                                className={`w-3.5 h-3.5 text-gray-400 transition-transform ${isOpen ? 'rotate-180' : ''}`}
                                                fill="none" viewBox="0 0 24 24" stroke="currentColor"
                                            >
                                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                                            </svg>
                                        </span>
                                    </button>

                                    {isOpen && (
                                        <div className="bg-gray-50 border-t border-gray-100 divide-y divide-gray-100">
                                            {ch.releases.map(r => (
                                                <div key={r.chapterId} className="px-5 py-2.5 pl-14 flex items-center gap-2">
                                                    <LangFlag code={r.language} />
                                                    <span className="text-sm text-gray-700 flex-1 truncate">
                                                        {r.scanGroup || <span className="italic text-gray-400">Unknown group</span>}
                                                    </span>
                                                    {r.quality && r.quality !== 'Unknown' && (
                                                        <span className="text-[10px] font-medium text-amber-700 bg-amber-50 px-1.5 py-0.5 rounded shrink-0">
                                                            {r.quality}
                                                        </span>
                                                    )}
                                                    {r.createdUtc && (
                                                        <span className="text-[10px] text-gray-400 whitespace-nowrap shrink-0">
                                                            {formatDate(r.createdUtc)}
                                                        </span>
                                                    )}
                                                </div>
                                            ))}
                                        </div>
                                    )}
                                </div>
                            );
                        })}
                    </div>
                </div>
            )}
        </div>
    );
}
