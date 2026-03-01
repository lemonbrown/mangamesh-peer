import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { getSubscriptions, removeSubscription, addSubscription } from '../api/subscriptions';
import { getSeriesDetails, getSeriesChapters } from '../api/series';
import type { Subscription, SeriesDetailsResponse, ChapterSummaryResponse } from '../types/api';

interface EnrichedSubscription {
    sub: Subscription;
    details: SeriesDetailsResponse | null;
    recentChapters: ChapterSummaryResponse[];
}

const LANGUAGES = [
    { code: 'en', label: 'English' },
    { code: 'ja', label: 'Japanese' },
    { code: 'es', label: 'Spanish' },
    { code: 'fr', label: 'French' },
    { code: 'de', label: 'German' },
    { code: 'pt', label: 'Portuguese' },
    { code: 'zh', label: 'Chinese' },
    { code: 'ko', label: 'Korean' },
    { code: 'it', label: 'Italian' },
    { code: 'ru', label: 'Russian' },
];

export default function Subscriptions() {
    const [items, setItems] = useState<EnrichedSubscription[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    async function load() {
        setLoading(true);
        setError(null);
        try {
            const subs = await getSubscriptions();
            const enriched = await Promise.all(
                subs.map(async (sub): Promise<EnrichedSubscription> => {
                    try {
                        const [details, chapters] = await Promise.all([
                            getSeriesDetails(sub.seriesId),
                            getSeriesChapters(sub.seriesId),
                        ]);
                        const recentChapters = chapters
                            .sort((a, b) => (b.chapterNumber || 0) - (a.chapterNumber || 0))
                            .slice(0, 3);
                        return { sub, details, recentChapters };
                    } catch {
                        return { sub, details: null, recentChapters: [] };
                    }
                })
            );
            setItems(enriched);
        } catch {
            setError('Failed to load subscriptions. Is the peer running?');
        } finally {
            setLoading(false);
        }
    }

    useEffect(() => { load(); }, []);

    async function handleRemove(sub: Subscription, title: string) {
        if (!confirm(`Unsubscribe from "${title}"?`)) return;
        try {
            await removeSubscription(sub.seriesId);
            setItems(prev => prev.filter(i => i.sub.seriesId !== sub.seriesId));
        } catch {
            alert('Failed to remove subscription.');
        }
    }

    async function handleLanguageChange(sub: Subscription, newLang: string) {
        try {
            await addSubscription(sub.seriesId, { ...sub, language: newLang });
            setItems(prev => prev.map(i =>
                i.sub.seriesId === sub.seriesId
                    ? { ...i, sub: { ...i.sub, language: newLang } }
                    : i
            ));
        } catch {
            alert('Failed to update language preference.');
        }
    }

    const timeAgo = (dateStr?: string) => {
        if (!dateStr) return '';
        const diffMs = Date.now() - new Date(dateStr).getTime();
        const diffMin = Math.floor(diffMs / 60000);
        const diffHour = Math.floor(diffMin / 60);
        const diffDay = Math.floor(diffHour / 24);
        if (diffMin < 1) return 'Just now';
        if (diffMin < 60) return `${diffMin}m ago`;
        if (diffHour < 24) return `${diffHour}h ago`;
        if (diffDay < 7) return `${diffDay}d ago`;
        return new Date(dateStr).toLocaleDateString();
    };

    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between">
                <h1 className="text-2xl font-bold text-gray-900">Subscriptions</h1>
                <Link to="/series" className="text-sm text-blue-600 hover:underline">
                    + Browse to subscribe
                </Link>
            </div>

            {loading && (
                <div className="space-y-3">
                    {[1, 2, 3].map(i => (
                        <div key={i} className="h-28 bg-gray-100 rounded-lg animate-pulse" />
                    ))}
                </div>
            )}

            {error && (
                <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-4 text-sm">
                    {error}
                </div>
            )}

            {!loading && !error && items.length === 0 && (
                <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-12 text-center">
                    <svg className="mx-auto w-10 h-10 text-gray-300 mb-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M5 5a2 2 0 012-2h10a2 2 0 012 2v16l-7-3.5L5 21V5z" />
                    </svg>
                    <p className="text-gray-500 font-medium">No subscriptions yet.</p>
                    <p className="text-gray-400 text-sm mt-1">Browse series and click Subscribe to start tracking them.</p>
                    <Link to="/series" className="mt-4 inline-block px-4 py-2 bg-blue-600 text-white rounded-md text-sm font-medium hover:bg-blue-700 transition-colors">
                        Browse Series
                    </Link>
                </div>
            )}

            <div className="space-y-3">
                {items.map(({ sub, details, recentChapters }) => {
                    const title = details?.title || sub.seriesId;
                    const externalMangaId = details?.externalMangaId;

                    return (
                        <div key={sub.seriesId} className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
                            <div className="flex gap-4 p-4">
                                {/* Cover */}
                                <div className="shrink-0 w-16 rounded overflow-hidden bg-gray-100 self-start relative flex items-center justify-center" style={{ minHeight: '5.5rem' }}>
                                    <svg className="w-6 h-6 text-gray-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253" />
                                    </svg>
                                    {externalMangaId && (
                                        <img
                                            src={`/covers/${externalMangaId}.thumb.webp`}
                                            alt={title}
                                            className="absolute inset-0 w-full h-full object-cover"
                                            onError={(e) => { (e.target as HTMLImageElement).style.display = 'none'; }}
                                        />
                                    )}
                                </div>

                                {/* Content */}
                                <div className="flex-1 min-w-0">
                                    <div className="flex items-start justify-between gap-3">
                                        <Link
                                            to={`/series/${sub.seriesId}`}
                                            className="font-semibold text-gray-900 hover:text-blue-600 transition-colors truncate"
                                        >
                                            {title}
                                        </Link>
                                        <button
                                            onClick={() => handleRemove(sub, title)}
                                            className="shrink-0 text-xs text-gray-400 hover:text-red-500 transition-colors px-2 py-1 rounded hover:bg-red-50"
                                        >
                                            Unsubscribe
                                        </button>
                                    </div>

                                    {/* Language selector + auto-fetch badge */}
                                    <div className="flex items-center gap-3 mt-2">
                                        <select
                                            value={sub.language}
                                            onChange={e => handleLanguageChange(sub, e.target.value)}
                                            className="text-xs border border-gray-200 rounded px-2 py-1 bg-white text-gray-700 focus:outline-none focus:ring-1 focus:ring-blue-300"
                                            title="Preferred language"
                                        >
                                            {LANGUAGES.map(l => (
                                                <option key={l.code} value={l.code}>{l.label}</option>
                                            ))}
                                        </select>
                                        {sub.subscribedAt && (
                                            <span className="text-xs text-gray-400">
                                                Since {timeAgo(sub.subscribedAt)}
                                            </span>
                                        )}
                                    </div>

                                    {/* Recent chapters */}
                                    {recentChapters.length > 0 && (
                                        <div className="mt-3 space-y-1">
                                            {recentChapters.map(ch => (
                                                <Link
                                                    key={ch.chapterId}
                                                    to={`/series/${sub.seriesId}/read/${ch.chapterId}`}
                                                    className="flex items-center gap-2 text-sm text-gray-600 hover:text-blue-600 group/ch"
                                                >
                                                    <span className="text-xs font-medium bg-gray-100 px-1.5 py-0.5 rounded min-w-[3rem] text-center">
                                                        Ch. {ch.chapterNumber}
                                                    </span>
                                                    {ch.title && <span className="text-gray-500 truncate">- {ch.title}</span>}
                                                    <span className="ml-auto text-xs text-gray-300 group-hover/ch:text-blue-400 transition-colors shrink-0">
                                                        {timeAgo(ch.uploadedAt)}
                                                    </span>
                                                </Link>
                                            ))}
                                        </div>
                                    )}
                                </div>
                            </div>
                        </div>
                    );
                })}
            </div>
        </div>
    );
}
