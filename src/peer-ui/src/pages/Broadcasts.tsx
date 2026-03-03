import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { getBroadcasts } from '../api/broadcasts';
import { groupBySeries } from '../utils/broadcastUtils';
import type { SeriesEntry } from '../utils/broadcastUtils';
import { langCountryCode } from '../utils/language';

function LangFlag({ code }: { code: string }) {
    const country = langCountryCode(code);
    if (!country) return <span className="text-xs text-gray-400">{code}</span>;
    return <span className={`fi fi-${country} rounded-sm text-base`} title={code} />;
}

export default function Broadcasts() {
    const [series, setSeries] = useState<SeriesEntry[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const load = useCallback(async () => {
        setLoading(true);
        setError(null);
        try {
            setSeries(groupBySeries(await getBroadcasts()));
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Failed to fetch peer broadcasts.');
        } finally {
            setLoading(false);
        }
    }, []);

    useEffect(() => { load(); }, [load]);

    return (
        <div className="space-y-6 pb-12">
            <div className="flex items-start justify-between">
                <div>
                    <h1 className="text-3xl font-bold text-gray-900 mb-1">Broadcasts</h1>
                    <p className="text-gray-500">Series discovered from peers in the DHT — not necessarily tracked by the index.</p>
                </div>
                <button
                    onClick={load}
                    disabled={loading}
                    className="flex items-center gap-1.5 px-3 py-2 rounded-md text-sm font-medium text-gray-600 hover:bg-gray-100 transition-colors disabled:opacity-50"
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
                <div className="grid grid-cols-1 gap-3">
                    {[1, 2, 3, 4, 5].map(i => (
                        <div key={i} className="bg-white rounded-xl border border-gray-100 shadow-sm p-4 animate-pulse flex items-center gap-4">
                            <div className="w-12 h-16 bg-gray-200 rounded shrink-0" />
                            <div className="flex-1 space-y-2">
                                <div className="h-4 bg-gray-200 rounded w-48" />
                                <div className="h-3 bg-gray-100 rounded w-24" />
                            </div>
                            <div className="h-6 bg-gray-100 rounded w-16" />
                        </div>
                    ))}
                </div>
            ) : series.length === 0 ? (
                <div className="text-gray-500 italic p-12 text-center bg-gray-50 rounded-xl border border-dashed border-gray-200">
                    No peers discovered in DHT yet. Try again after the node bootstraps.
                </div>
            ) : (
                <div className="grid grid-cols-1 gap-3">
                    {series.map(entry => {
                        const latestCh = entry.chapters.at(-1);
                        const allLangs = [...new Set(entry.chapters.flatMap(ch => ch.releases.map(r => r.language)).filter(Boolean))];

                        return (
                            <Link
                                key={entry.seriesId}
                                to={`/broadcasts/${entry.seriesId}`}
                                state={entry}
                                className="bg-white rounded-xl border border-gray-100 shadow-sm px-4 py-3 flex items-center gap-4 hover:border-blue-200 hover:bg-blue-50/30 transition-colors group"
                            >
                                {entry.coverUrl ? (
                                    <img
                                        src={entry.coverUrl}
                                        alt=""
                                        className="w-12 h-16 object-cover rounded shrink-0 bg-gray-100"
                                    />
                                ) : (
                                    <div className="w-12 h-16 rounded shrink-0 bg-gray-100 flex items-center justify-center text-gray-300">
                                        <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z" />
                                        </svg>
                                    </div>
                                )}
                                <div className="flex-1 min-w-0">
                                    <p className="font-semibold text-gray-900 group-hover:text-blue-700 truncate transition-colors">
                                        {entry.seriesTitle ?? entry.seriesId}
                                    </p>
                                    <p className="text-xs text-gray-400 mt-0.5">
                                        {entry.chapters.length} {entry.chapters.length === 1 ? 'chapter' : 'chapters'} &middot; {entry.peerCount} {entry.peerCount === 1 ? 'peer' : 'peers'}
                                    </p>
                                </div>
                                <div className="flex items-center gap-2 shrink-0">
                                    {allLangs.map(lang => <LangFlag key={lang} code={lang} />)}
                                    {latestCh && (
                                        <span className="text-xs font-mono font-semibold text-blue-600 bg-blue-50 px-2 py-1 rounded-lg">
                                            Ch. {Number.isInteger(latestCh.chapterNumber) ? latestCh.chapterNumber : latestCh.chapterNumber.toFixed(1)}
                                        </span>
                                    )}
                                    <svg className="w-4 h-4 text-gray-300 group-hover:text-blue-400 transition-colors" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                                    </svg>
                                </div>
                            </Link>
                        );
                    })}
                </div>
            )}
        </div>
    );
}
