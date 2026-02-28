import { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { getSeriesChapters, getSeriesDetails, getChapterDetails } from '../api/series';
import { getSubscriptions, subscribe, unsubscribe } from '../api/subscriptions';
import { getManifestFlagSummaries, loadLocallyFlagged, saveLocallyFlagged } from '../api/flags';
import type { ChapterSummaryResponse, Subscription, SeriesDetailsResponse, ChapterManifest, FlagSummary } from '../types/api';
import LangFlag from '../components/LangFlag';
import FlagModal from '../components/FlagModal';

export default function SeriesDetails() {
    const { seriesId } = useParams<{ seriesId: string }>();
    const [chapters, setChapters] = useState<ChapterSummaryResponse[]>([]);
    const [chapterManifests, setChapterManifests] = useState<Record<string, ChapterManifest[]>>({});
    const [seriesInfo, setSeriesInfo] = useState<SeriesDetailsResponse | null>(null);
    const [subscription, setSubscription] = useState<Subscription | null>(null);
    const [loading, setLoading] = useState(true);
    const [manifestsLoading, setManifestsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // Flag state
    const [locallyFlagged, setLocallyFlagged] = useState<Set<string>>(() => loadLocallyFlagged());
    const [flagWarnings, setFlagWarnings] = useState<Record<string, FlagSummary>>({});
    const [flagModal, setFlagModal] = useState<{ manifestHash: string; chapterLabel: string } | null>(null);

    useEffect(() => {
        async function load() {
            if (!seriesId) return;
            try {
                const [chapterData, subs, details] = await Promise.all([
                    getSeriesChapters(seriesId),
                    getSubscriptions(),
                    getSeriesDetails(seriesId)
                ]);
                const sortedChapters = (chapterData || []).sort((a, b) => {
                    return (b.chapterNumber || 0) - (a.chapterNumber || 0);
                });
                setChapters(sortedChapters);
                setSeriesInfo(details);
                const sub = subs.find(s => s.seriesId === seriesId);
                setSubscription(sub || null);

                // Fetch manifests for each chapter
                if (chapterData.length > 0) {
                    setManifestsLoading(true);

                    const detailsResults = await Promise.allSettled(
                        chapterData.map(ch => getChapterDetails(seriesId, ch.chapterId))
                    );

                    const manifestsMap: Record<string, ChapterManifest[]> = {};
                    const titleUpdates: Record<string, string> = {};
                    const allHashes: string[] = [];

                    detailsResults.forEach((result, index) => {
                        if (result.status === 'fulfilled') {
                            const chapterId = chapterData[index].chapterId;
                            const val = result.value;
                            const manifests: ChapterManifest[] = val.manifests || (val as any).Manifests || [];
                            manifestsMap[chapterId] = manifests;
                            manifests.forEach(m => {
                                const h = m.manifestHash || (m as any).ManifestHash;
                                if (h) allHashes.push(h);
                            });

                            const title = val.title || (val as any).Title;
                            if (title) {
                                titleUpdates[chapterId] = title;
                            }
                        }
                    });

                    setChapterManifests(manifestsMap);

                    if (Object.keys(titleUpdates).length > 0) {
                        setChapters(prev => prev.map(ch => ({
                            ...ch,
                            title: titleUpdates[ch.chapterId] || ch.title
                        })));
                    }

                    // Fetch flag summaries (silently fails if backend not ready)
                    if (allHashes.length > 0) {
                        getManifestFlagSummaries(allHashes).then(summaries => {
                            setFlagWarnings(summaries);
                        });
                    }

                    setManifestsLoading(false);
                }
            } catch (e) {
                setError('Failed to load series data');
            } finally {
                setLoading(false);
            }
        }
        load();
    }, [seriesId]);

    async function handleSubscribeToggle() {
        if (!seriesId) return;
        try {
            if (subscription) {
                if (confirm('Are you sure you want to unsubscribe?')) {
                    await unsubscribe(seriesId);
                    setSubscription(null);
                }
            } else {
                await subscribe(seriesId);
                const subs = await getSubscriptions();
                setSubscription(subs.find(s => s.seriesId === seriesId) || null);
            }
        } catch (e) {
            alert('Failed to update subscription');
        }
    }

    function handleFlagSubmitted(manifestHash: string) {
        setLocallyFlagged(prev => {
            const next = new Set(prev);
            next.add(manifestHash);
            saveLocallyFlagged(next);
            return next;
        });
    }

    if (loading) return <div className="p-8 text-gray-500">Loading chapters...</div>;
    if (error) return <div className="p-8 text-red-500">{error}</div>;

    return (
        <div className="space-y-6">
            {/* Series header — cover + metadata */}
            <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
                <div className="flex gap-5 p-5">
                    {/* Cover art */}
                    <div className="shrink-0 w-28 rounded overflow-hidden bg-gray-100 self-start relative flex items-center justify-center" style={{ minHeight: '10rem' }}>
                        <svg className="w-8 h-8 text-gray-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253" />
                        </svg>
                        {seriesInfo?.externalMangaId && (
                            <img
                                src={`/covers/${seriesInfo.externalMangaId}.card.webp`}
                                alt={seriesInfo.title}
                                className="absolute inset-0 w-full h-full object-cover"
                                onError={(e) => { (e.target as HTMLImageElement).style.display = 'none'; }}
                            />
                        )}
                    </div>

                    {/* Info */}
                    <div className="flex-1 min-w-0">
                        <h1 className="text-2xl font-bold text-gray-900 leading-tight">{seriesInfo?.title || seriesId}</h1>
                        {seriesInfo?.author && (
                            <div className="text-sm text-gray-500 mt-1">{seriesInfo.author}</div>
                        )}
                        <div className="flex flex-wrap gap-3 mt-3 text-sm text-gray-500">
                            <span><span className="font-semibold text-gray-700">{chapters.length}</span> chapters</span>
                            {seriesInfo?.seedCount !== undefined && (
                                <span><span className="font-semibold text-green-600">{seriesInfo.seedCount}</span> seeds</span>
                            )}
                        </div>
                        <div className="flex items-center gap-3 mt-4">
                            <button
                                onClick={handleSubscribeToggle}
                                className={`px-4 py-2 rounded-md text-sm font-medium transition-colors ${subscription
                                    ? 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                                    : 'bg-blue-600 text-white hover:bg-blue-700 shadow-sm'
                                    }`}
                            >
                                {subscription ? 'Subscribed' : 'Subscribe'}
                            </button>
                            <Link to="/series" className="text-blue-600 hover:underline text-sm">
                                ← Series
                            </Link>
                        </div>
                    </div>
                </div>
            </div>

            <div className="space-y-4">
                {chapters.length === 0 ? (
                    <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-8 text-center text-gray-500">No chapters found.</div>
                ) : (
                    chapters.map((chapter) => {
                        const manifests = chapterManifests[chapter.chapterId] || [];

                        return (
                            <div key={chapter.chapterId} className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
                                <div className="px-4 py-2 bg-gray-50 border-b border-gray-100 flex items-center justify-between">
                                    <h3 className="text-sm font-bold text-gray-700">
                                        Chapter {chapter.chapterNumber}
                                        {chapter.title && <span className="ml-2 font-normal text-gray-500">- {chapter.title}</span>}
                                    </h3>
                                    {chapter.volume && <span className="text-[10px] font-medium text-gray-400 uppercase">Vol. {chapter.volume}</span>}
                                </div>

                                <div className="divide-y divide-gray-50">
                                    {manifestsLoading && manifests.length === 0 ? (
                                        <div className="p-4 text-sm text-gray-400 italic">
                                            Loading available versions...
                                        </div>
                                    ) : manifests.length === 0 ? (
                                        <div className="p-4 text-sm text-gray-400 italic">
                                            No versions available.
                                        </div>
                                    ) : (
                                        manifests.map((manifest) => {
                                            const mHash = manifest.manifestHash || (manifest as any).ManifestHash;
                                            const mLang = manifest.language || (manifest as any).Language;
                                            const mQuality = manifest.quality || (manifest as any).Quality;
                                            const mScanGroup = manifest.scanGroup || (manifest as any).ScanGroup;
                                            let mIsVerified = manifest.isVerified !== undefined ? manifest.isVerified : (manifest as any).IsVerified;

                                            // Temporary Mock: Mark 'opscan' as verified for demonstration
                                            if (mScanGroup?.toLowerCase().includes('opscan')) {
                                                mIsVerified = true;
                                            }

                                            const mUploadedAt = manifest.uploadedAt || (manifest as any).UploadedAt;
                                            const chapterLabel = `Chapter ${chapter.chapterNumber}${chapter.title ? ` — ${chapter.title}` : ''} · ${mScanGroup || mLang}`;
                                            const isPeerFlagged = flagWarnings[mHash]?.hasMultiplePeerFlags === true;
                                            const isLocalFlagged = locallyFlagged.has(mHash);

                                            return (
                                                <div key={mHash} className="group">
                                                    {/* Warning badge — shown when multiple peers have flagged this manifest */}
                                                    {isPeerFlagged && (
                                                        <div className="mx-3 mt-2.5 flex items-center gap-2 px-3 py-2 bg-amber-50 border border-amber-200 rounded-md">
                                                            <svg className="w-3.5 h-3.5 text-amber-500 shrink-0" fill="currentColor" viewBox="0 0 20 20">
                                                                <path fillRule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
                                                            </svg>
                                                            <span className="text-xs font-medium text-amber-700">
                                                                Multiple peers report issues with this chapter
                                                            </span>
                                                        </div>
                                                    )}

                                                    {/* Row: link + flag button */}
                                                    <div className="flex items-stretch hover:bg-gray-50 transition-colors">
                                                        <Link
                                                            to={`/series/${seriesId}/read/${chapter.chapterId}?manifest=${mHash}`}
                                                            className="flex-1 p-4 flex justify-between items-center min-w-0"
                                                        >
                                                            <div className="flex-1 min-w-0">
                                                                <div className="flex items-center gap-3">
                                                                    <span className="bg-blue-100 text-blue-700 px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider flex items-center gap-1">
                                                                        <LangFlag code={mLang} /> {mLang}
                                                                    </span>
                                                                    <span className="bg-gray-100 text-gray-600 px-2 py-0.5 rounded text-[10px] font-medium uppercase tracking-wider">
                                                                        {mQuality}
                                                                    </span>
                                                                    {mScanGroup && (
                                                                        <div className="flex items-center gap-1 min-w-0">
                                                                            <span className="text-sm font-medium text-gray-700 truncate max-w-[200px]">
                                                                                {mScanGroup}
                                                                            </span>
                                                                            {mIsVerified && (
                                                                                <svg className="w-3.5 h-3.5 text-blue-500 fill-current shrink-0" viewBox="0 0 20 20">
                                                                                    <path d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" />
                                                                                </svg>
                                                                            )}
                                                                        </div>
                                                                    )}
                                                                </div>
                                                                <div className="text-[11px] text-gray-500 mt-1 flex items-center gap-2">
                                                                    <span>Uploaded {new Date(mUploadedAt).toLocaleDateString()}</span>
                                                                </div>
                                                            </div>
                                                            <div className="flex items-center gap-4 ml-4">
                                                                <span className="text-gray-400 text-[10px] font-mono opacity-0 group-hover:opacity-100 transition-opacity">
                                                                    {mHash?.substring(0, 8)}
                                                                </span>
                                                                <svg className="w-4 h-4 text-gray-300 group-hover:text-blue-400 transition-colors" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                                                                </svg>
                                                            </div>
                                                        </Link>

                                                        {/* Flag button */}
                                                        <button
                                                            type="button"
                                                            onClick={() => setFlagModal({ manifestHash: mHash, chapterLabel })}
                                                            title={isLocalFlagged ? 'You reported an issue with this chapter' : 'Flag this chapter'}
                                                            className={`flex items-center px-3 border-l border-gray-100 transition-colors shrink-0 ${
                                                                isLocalFlagged
                                                                    ? 'text-red-500'
                                                                    : 'text-gray-300 hover:text-red-400 opacity-0 group-hover:opacity-100'
                                                            }`}
                                                        >
                                                            <svg className="w-4 h-4" viewBox="0 0 24 24">
                                                                <line x1="5" y1="21" x2="5" y2="3" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
                                                                <path
                                                                    d="M5 3 L19 8 L5 13 Z"
                                                                    fill={isLocalFlagged ? 'currentColor' : 'none'}
                                                                    stroke="currentColor"
                                                                    strokeWidth="2"
                                                                    strokeLinejoin="round"
                                                                />
                                                            </svg>
                                                        </button>
                                                    </div>
                                                </div>
                                            );
                                        })
                                    )}
                                </div>
                            </div>
                        );
                    })
                )}
            </div>

            {/* Flag modal */}
            {flagModal && (
                <FlagModal
                    manifestHash={flagModal.manifestHash}
                    chapterLabel={flagModal.chapterLabel}
                    onClose={() => setFlagModal(null)}
                    onSubmitted={handleFlagSubmitted}
                />
            )}
        </div>
    );
}
