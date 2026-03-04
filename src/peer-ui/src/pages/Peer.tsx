import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { getLocalAllSeries } from '../api/storage';
import type { LocalSeriesSummary } from '../api/storage';

function formatBytes(bytes: number): string {
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export default function Peer() {
    const [series, setSeries] = useState<LocalSeriesSummary[]>([]);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        getLocalAllSeries()
            .then(setSeries)
            .catch(e => console.error('Failed to load local series', e))
            .finally(() => setLoading(false));
    }, []);

    return (
        <div className="space-y-6 pb-12">
            <div>
                <h1 className="text-3xl font-bold text-gray-900 mb-1">Local Library</h1>
                <p className="text-gray-500">Series seeded by this peer node.</p>
            </div>

            {loading ? (
                <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-4">
                    {[1, 2, 3, 4, 5].map(i => (
                        <div key={i} className="aspect-[2/3] bg-gray-100 rounded-xl animate-pulse" />
                    ))}
                </div>
            ) : series.length === 0 ? (
                <div className="text-gray-500 italic p-12 text-center bg-gray-50 rounded-xl border border-dashed border-gray-200">
                    No series found on this peer. Import chapters via the Publish tab to start seeding.
                </div>
            ) : (
                <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-4">
                    {series.map(s => (
                        <Link
                            key={s.seriesId}
                            to={`/series/${s.seriesId}`}
                            className="group flex flex-col bg-white rounded-xl overflow-hidden border border-gray-100 shadow-sm hover:shadow-md transition-all duration-200 hover:-translate-y-1 h-full"
                        >
                            <div className="relative aspect-[2/3] bg-gray-100 overflow-hidden flex items-center justify-center text-gray-300">
                                {s.externalMangaId ? (
                                    <img
                                        src={`/covers/${s.externalMangaId}.card.webp`}
                                        alt={s.seriesTitle ?? s.seriesId}
                                        className="w-full h-full object-cover"
                                    />
                                ) : (
                                    <svg className="w-10 h-10" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1} d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253" />
                                    </svg>
                                )}
                                <div className="absolute inset-0 bg-gradient-to-t from-black/60 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-300 flex items-end p-3">
                                    <span className="text-white text-sm font-medium">View Series</span>
                                </div>
                            </div>

                            <div className="p-3 flex flex-col flex-1">
                                <h3 className="font-semibold text-gray-900 leading-tight group-hover:text-blue-600 transition-colors line-clamp-2 mb-2">
                                    {s.seriesTitle ?? s.seriesId}
                                </h3>
                                <div className="mt-auto flex items-center justify-between text-xs text-gray-500">
                                    <span className="text-blue-600 font-medium">{s.chapterCount} ch</span>
                                    <span>{formatBytes(s.totalSizeBytes)}</span>
                                </div>
                            </div>
                        </Link>
                    ))}
                </div>
            )}
        </div>
    );
}
