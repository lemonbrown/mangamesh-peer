import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { searchSeries, getSeriesChapters } from '../api/series';
import type { SeriesSearchResult, ChapterSummaryResponse } from '../types/api';

export default function Series() {
    const [results, setResults] = useState<SeriesSearchResult[]>([]);
    const [popular, setPopular] = useState<SeriesSearchResult[]>([]);
    const [recent, setRecent] = useState<{ series: SeriesSearchResult, chapters: ChapterSummaryResponse[] }[]>([]);
    const [loading, setLoading] = useState(false);
    const [initialLoading, setInitialLoading] = useState(true);
    const [query, setQuery] = useState('');

    useEffect(() => {
        loadInitialData();
    }, []);

    async function loadInitialData() {
        setInitialLoading(true);
        try {
            const [popData, recData] = await Promise.all([
                searchSeries('', 5, 0, 'popular'),
                searchSeries('', 5, 0, 'recent')
            ]);
            setPopular(popData);

            const recentWithChapters = await Promise.all(recData.map(async (s) => {
                try {
                    const chaps = await getSeriesChapters(s.seriesId);
                    const sorted = chaps.sort((a, b) => (b.chapterNumber || 0) - (a.chapterNumber || 0));
                    return { series: s, chapters: sorted.slice(0, 3) };
                } catch {
                    return { series: s, chapters: [] };
                }
            }));
            setRecent(recentWithChapters);
        } catch (e) {
            console.error("Failed to load initial data", e);
        } finally {
            setInitialLoading(false);
        }
    }

    async function handleSearch(q: string) {
        setQuery(q);
        if (!q) {
            setResults([]);
            return;
        }

        setLoading(true);
        try {
            const data = await searchSeries(q);
            setResults(data);
        } catch (e) {
            console.error(e);
        } finally {
            setLoading(false);
        }
    }

    const timeAgo = (dateStr?: string) => {
        if (!dateStr) return '';
        const date = new Date(dateStr);
        const now = new Date();
        const diffMs = now.getTime() - date.getTime();
        const diffMin = Math.floor(diffMs / 60000);
        const diffHour = Math.floor(diffMin / 60);
        const diffDay = Math.floor(diffHour / 24);

        if (diffMin < 1) return 'Just now';
        if (diffMin < 60) return `${diffMin}m ago`;
        if (diffHour < 24) return `${diffHour}h ago`;
        if (diffDay < 7) return `${diffDay}d ago`;
        return date.toLocaleDateString();
    };

    const PopularSeriesCard = ({ series }: { series: SeriesSearchResult }) => (
        <Link
            to={`/series/${series.seriesId}`}
            className="group flex flex-col bg-white rounded-xl overflow-hidden border border-gray-100 shadow-sm hover:shadow-md transition-all duration-200 hover:-translate-y-1 h-full"
        >
            <div className="relative aspect-[2/3] bg-gray-100 overflow-hidden">
                {series.externalMangaId ? (
                    <img
                        src={`/covers/${series.externalMangaId}.card.webp`}
                        alt={series.title}
                        className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
                        loading="lazy"
                        onError={(e) => { (e.target as HTMLImageElement).style.display = 'none'; }}
                    />
                ) : null}
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
                    {series.title}
                </h3>
                <div className="mt-auto flex items-center justify-between text-xs text-gray-500">
                    <span className="text-green-600 font-medium">{series.seedCount ?? 0} seeds</span>
                    <span>{series.chapterCount ?? 0} ch</span>
                </div>
            </div>
        </Link>
    );

    const SeriesListCard = ({ series, recentChapters }: { series: SeriesSearchResult, recentChapters?: ChapterSummaryResponse[] }) => (
        <div className="flex bg-white p-3 rounded-xl shadow-sm border border-gray-100 hover:shadow-md transition-all duration-200 group">
            <Link to={`/series/${series.seriesId}`} className="shrink-0 w-20 h-28 bg-gray-100 rounded-lg overflow-hidden border border-gray-200 relative flex items-center justify-center">
                <svg className="w-6 h-6 text-gray-300" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1} d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253" />
                </svg>
                {series.externalMangaId && (
                    <img
                        src={`/covers/${series.externalMangaId}.thumb.webp`}
                        alt={series.title}
                        className="absolute inset-0 w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                        loading="lazy"
                        onError={(e) => { (e.target as HTMLImageElement).style.display = 'none'; }}
                    />
                )}
            </Link>

            <div className="flex-1 ml-4 py-0.5 flex flex-col justify-between">
                <div>
                    <div className="flex justify-between items-start gap-2">
                        <Link
                            to={`/series/${series.seriesId}`}
                            className="font-bold text-gray-900 group-hover:text-blue-600 transition-colors line-clamp-1 text-lg"
                        >
                            {series.title}
                        </Link>
                        {series.lastUploadedAt && (
                            <span className="text-xs text-gray-400 whitespace-nowrap pt-1">
                                {timeAgo(series.lastUploadedAt)}
                            </span>
                        )}
                    </div>

                    {recentChapters && recentChapters.length > 0 ? (
                        <div className="mt-2 space-y-1">
                            {recentChapters.map(ch => (
                                <Link
                                    key={ch.chapterId}
                                    to={`/series/${series.seriesId}/read/${ch.chapterId}`}
                                    className="flex items-center gap-2 text-sm text-gray-600 hover:text-blue-600 group/chapter py-0.5"
                                >
                                    <span className="font-medium text-gray-800 bg-gray-100 px-1.5 py-0.5 rounded text-xs min-w-[3rem] text-center">
                                        Ch. {ch.chapterNumber}
                                    </span>
                                    {ch.title && <span className="text-gray-500 truncate">- {ch.title}</span>}
                                    <span className="text-xs text-gray-300 ml-auto group-hover/chapter:text-blue-400 transition-colors">
                                        {timeAgo(ch.uploadedAt)}
                                    </span>
                                </Link>
                            ))}
                        </div>
                    ) : (
                        <div className="mt-1 text-sm text-gray-400 italic">
                            {series.chapterCount ?? 0} ch · <span className="text-green-500">{series.seedCount ?? 0} seeds</span>
                        </div>
                    )}
                </div>
            </div>
        </div>
    );

    return (
        <div className="space-y-10 pb-12">
            <div className="flex flex-col gap-6">
                <div>
                    <h1 className="text-3xl font-bold text-gray-900 mb-1">Explore Series</h1>
                    <p className="text-gray-500">Find and read manga hosted on the MangaMesh network.</p>
                </div>

                <div className="w-full relative">
                    <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                        <svg className="h-5 w-5 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                        </svg>
                    </div>
                    <input
                        type="text"
                        className="block w-full pl-10 pr-3 py-2.5 border border-gray-200 rounded-xl bg-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-100 focus:border-blue-400 sm:text-sm transition-all shadow-sm"
                        placeholder="Search series..."
                        value={query}
                        onChange={(e) => handleSearch(e.target.value)}
                    />
                    {loading && (
                        <div className="absolute right-3 top-2.5 text-gray-400">
                            <svg className="animate-spin h-5 w-5" viewBox="0 0 24 24">
                                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                            </svg>
                        </div>
                    )}
                </div>
            </div>

            {!query ? (
                <div className="space-y-12">
                    <section>
                        <h2 className="text-xl font-bold text-gray-900 mb-5 flex items-center gap-2">
                            <svg className="w-5 h-5 text-yellow-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
                            </svg>
                            Popular Series
                        </h2>
                        {initialLoading ? (
                            <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-4">
                                {[1, 2, 3, 4, 5].map(i => <div key={i} className="aspect-[2/3] bg-gray-100 rounded-xl animate-pulse" />)}
                            </div>
                        ) : popular.length === 0 ? (
                            <div className="text-gray-500 italic p-6 text-center bg-gray-50 rounded-xl border border-dashed border-gray-200">No popular series found.</div>
                        ) : (
                            <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-y-8 gap-x-4">
                                {popular.map(s => <PopularSeriesCard key={s.seriesId} series={s} />)}
                            </div>
                        )}
                    </section>

                    <section>
                        <h2 className="text-xl font-bold text-gray-900 mb-5 flex items-center gap-2">
                            <svg className="w-5 h-5 text-blue-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                            Recently Updated
                        </h2>
                        {initialLoading ? (
                            <div className="space-y-4">
                                {[1, 2, 3].map(i => <div key={i} className="h-32 bg-gray-100 rounded-xl animate-pulse" />)}
                            </div>
                        ) : recent.length === 0 ? (
                            <div className="text-gray-500 italic p-6 text-center bg-gray-50 rounded-xl border border-dashed border-gray-200">No recently updated series found.</div>
                        ) : (
                            <div className="grid gap-4 max-w-4xl">
                                {recent.map(item => <SeriesListCard key={item.series.seriesId} series={item.series} recentChapters={item.chapters} />)}
                            </div>
                        )}
                    </section>
                </div>
            ) : (
                <div>
                    <h2 className="text-lg font-medium text-gray-900 mb-4">Search Results</h2>
                    {!loading && results.length === 0 && (
                        <div className="text-gray-500 italic text-center py-12 bg-gray-50 rounded-xl">No series found matching "{query}".</div>
                    )}
                    <div className="grid gap-4 max-w-4xl">
                        {results.map(series => <SeriesListCard key={series.seriesId} series={series} />)}
                    </div>
                </div>
            )}
        </div>
    );
}
