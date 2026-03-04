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
                <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-y-8 gap-x-4">
                    {series.map(entry => (
                        <Link
                            key={entry.seriesId}
                            to={`/broadcasts/${entry.seriesId}`}
                            state={entry}
                            className="group flex flex-col bg-white rounded-xl overflow-hidden border border-gray-100 shadow-sm hover:shadow-md transition-all duration-200 hover:-translate-y-1 h-full"
                        >
                            <div className="relative aspect-[2/3] bg-gray-100 overflow-hidden">
                                {entry.externalMangaId && (
                                    <img
                                        src={`/covers/${entry.externalMangaId}.card.webp`}
                                        alt={entry.seriesTitle ?? entry.seriesId}
                                        className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
                                        loading="lazy"
                                        onError={(e) => { (e.target as HTMLImageElement).style.display = 'none'; }}
                                    />
                                )}
                                <div className="absolute inset-0 flex items-center justify-center text-gray-300 -z-0">
                                    <svg className="w-10 h-10" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1} d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253" />
                                    </svg>
                                </div>
                                <div className="absolute inset-0 bg-gradient-to-t from-black/60 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-300 flex items-end p-3">
                                    <span className="text-white text-sm font-medium">View Series</span>
                                </div>
                            </div>
                            <div className="p-3 flex flex-col flex-1">
                                <h3 className="font-semibold text-gray-900 leading-tight group-hover:text-blue-600 transition-colors line-clamp-2 mb-2">
                                    {entry.seriesTitle ?? entry.seriesId}
                                </h3>
                                <div className="mt-auto flex items-center justify-between text-xs text-gray-500">
                                    <span className="text-green-600 font-medium">{entry.peerCount} {entry.peerCount === 1 ? 'peer' : 'peers'}</span>
                                    <span>{entry.chapters.length} ch</span>
                                </div>
                            </div>
                        </Link>
                    ))}
                </div>
            )}
        </div>
    );
}
