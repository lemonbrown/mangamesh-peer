import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { getBroadcasts } from '../api/broadcasts';
import { groupBySeries } from '../utils/broadcastUtils';
import type { SeriesEntry } from '../utils/broadcastUtils';

export default function Broadcasts() {
    const [series, setSeries] = useState<SeriesEntry[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const load = useCallback(async () => {
        setLoading(true);
        setError(null);
        try {
            const grouped = groupBySeries(await getBroadcasts());
            // Sort series descending by the createdUtc of their latest chapter release
            grouped.sort((a, b) => {
                const getLatestDate = (series: SeriesEntry) => {
                    let latest = 0;
                    for (const ch of series.chapters) {
                        for (const r of ch.releases) {
                            if (r.createdUtc) {
                                const t = new Date(r.createdUtc).getTime();
                                if (t > latest) latest = t;
                            }
                        }
                    }
                    return latest;
                };
                return getLatestDate(b) - getLatestDate(a);
            });
            setSeries(grouped);
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
                    <p className="text-gray-500">Series broadcasted by peers on the network</p>
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
                <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-4">
                    {[1, 2, 3, 4, 5].map(i => (
                        <div key={i} className="aspect-[2/3] bg-gray-100 rounded-xl animate-pulse" />
                    ))}
                </div>
            ) : series.length === 0 ? (
                <div className="text-gray-500 italic p-12 text-center bg-gray-50 rounded-xl border border-dashed border-gray-200">
                    No peers discovered in DHT yet. Try again after the node bootstraps.
                </div>
            ) : (
                <div className="space-y-3">
                    {series.map(entry => (
                        <Link
                            key={entry.seriesId}
                            to={`/broadcasts/${entry.seriesId}`}
                            state={entry}
                            className="group flex flex-row items-center bg-white rounded-xl overflow-hidden border border-gray-100 shadow-sm hover:shadow-md transition-all duration-200 hover:-translate-y-[1px]"
                        >
                            <div className="relative w-24 h-32 bg-gray-100 shrink-0 overflow-hidden flex items-center justify-center">
                                {entry.externalMangaId ? (
                                    <img
                                        src={`/covers/${entry.externalMangaId}.thumb.webp`}
                                        alt={entry.seriesTitle ?? entry.seriesId}
                                        className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
                                        loading="lazy"
                                        onError={(e) => { (e.target as HTMLImageElement).style.display = 'none'; }}
                                    />
                                ) : (
                                    <svg className="w-8 h-8 text-gray-300" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1} d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253" />
                                    </svg>
                                )}
                            </div>
                            <div className="p-4 flex flex-col flex-1 h-full min-w-0">
                                <h3 className="text-lg font-semibold text-gray-900 leading-tight group-hover:text-blue-600 transition-colors truncate mb-1">
                                    {entry.seriesTitle ?? entry.seriesId}
                                </h3>
                                <div className="mt-auto flex items-center gap-4 text-sm text-gray-500">
                                    <span className="flex items-center gap-1.5">
                                        <span className="w-1.5 h-1.5 rounded-full bg-green-500"></span>
                                        <span className="font-medium text-gray-700">{entry.peerCount}</span> {entry.peerCount === 1 ? 'peer seating' : 'peers seeding'}
                                    </span>
                                    <span className="flex items-center gap-1.5">
                                        <svg className="w-4 h-4 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 002-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
                                        </svg>
                                        <span className="font-medium text-gray-700">{entry.chapters.length}</span> chapters
                                    </span>
                                </div>
                            </div>
                            <div className="pr-4 shrink-0 text-gray-300 group-hover:text-blue-500 transition-colors">
                                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                                </svg>
                            </div>
                        </Link>
                    ))}
                </div>
            )}
        </div>
    );
}
