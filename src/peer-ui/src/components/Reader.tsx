import { useEffect, useState } from 'react';
import { useParams, Link, useSearchParams } from 'react-router-dom';
import { readChapter, getChapterDetails } from '../api/series';
import { getManifestFlagSummary, loadLocallyFlagged, saveLocallyFlagged } from '../api/flags';
import type { FullChapterManifest } from '../types/api';
import FlagModal from './FlagModal';
import ReportPeerModal from './ReportPeerModal';

export default function Reader() {
    const { seriesId, chapterId } = useParams<{ seriesId: string, chapterId: string }>();
    const [searchParams] = useSearchParams();
    const manifestHash = searchParams.get('manifest');

    const [manifest, setManifest] = useState<FullChapterManifest | null>(null);
    const [resolvedHash, setResolvedHash] = useState<string | null>(manifestHash);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    // Flag state
    const [peerWarning, setPeerWarning] = useState(false);
    const [warningDismissed, setWarningDismissed] = useState(false);
    const [isLocallyFlagged, setIsLocallyFlagged] = useState(false);
    const [flagModalOpen, setFlagModalOpen] = useState(false);
    const [reportPeerModalOpen, setReportPeerModalOpen] = useState(false);

    useEffect(() => {
        async function load() {
            if (!seriesId || !chapterId) {
                setError('Missing required parameters (seriesId or chapterId)');
                setLoading(false);
                return;
            }

            // Auto-select first manifest if none specified in URL
            let resolvedManifestHash = manifestHash;
            if (!resolvedManifestHash) {
                try {
                    const chapterDetails = await getChapterDetails(seriesId, chapterId);
                    const manifests = chapterDetails.manifests ? [...chapterDetails.manifests] : [];
                    if (manifests.length === 0) {
                        setError('No manifests available for this chapter.');
                        setLoading(false);
                        return;
                    }
                    resolvedManifestHash = manifests[0].manifestHash;
                } catch (e) {
                    console.error(e);
                    setError('Failed to load chapter details.');
                    setLoading(false);
                    return;
                }
            }

            setResolvedHash(resolvedManifestHash);

            // Check local flag state
            const flagged = loadLocallyFlagged();
            setIsLocallyFlagged(flagged.has(resolvedManifestHash));

            try {
                // This call ensures content is synced locally via P2P
                const data = await readChapter(seriesId, chapterId, resolvedManifestHash);
                setManifest(data);
            } catch (e) {
                console.error(e);
                setError('Failed to load chapter content. Ensure peers are online.');
            } finally {
                setLoading(false);
            }

            // Fetch flag summary (silently, after content load)
            getManifestFlagSummary(resolvedManifestHash).then(summary => {
                if (summary?.hasMultiplePeerFlags) {
                    setPeerWarning(true);
                }
            });
        }
        load();
    }, [seriesId, chapterId, manifestHash]);

    function handleFlagSubmitted(hash: string) {
        setIsLocallyFlagged(true);
        const flagged = loadLocallyFlagged();
        flagged.add(hash);
        saveLocallyFlagged(flagged);
        setFlagModalOpen(false);
    }

    if (loading) return (
        <div className="flex flex-col items-center justify-center min-h-screen bg-gray-100">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mb-4"></div>
            <div className="text-gray-600 font-medium">Syncing chapter from peers...</div>
        </div>
    );

    if (error) return (
        <div className="min-h-screen bg-gray-100 flex items-center justify-center p-4">
            <div className="bg-white p-8 rounded-lg shadow-md max-w-md w-full text-center">
                <div className="text-red-500 mb-4">
                    <svg className="w-12 h-12 mx-auto" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                    </svg>
                </div>
                <h2 className="text-xl font-bold text-gray-900 mb-2">Error Loading Chapter</h2>
                <p className="text-gray-600 mb-6">{error}</p>
                <Link to={`/series/${seriesId}`} className="inline-block bg-blue-600 text-white px-6 py-2 rounded-md hover:bg-blue-700 transition-colors">
                    Back to Series
                </Link>
            </div>
        </div>
    );

    if (!manifest) return null;

    const chapterLabel = `Chapter ${manifest.chapterNumber}${manifest.scanGroup ? ` · ${manifest.scanGroup}` : ''}`;

    return (
        <div className="bg-black min-h-screen pb-20">
            {/* Sticky Header */}
            <div className="sticky top-0 z-10 bg-gray-900/90 border-b border-gray-800 px-4 py-3 flex items-center justify-between shadow-lg backdrop-blur-sm transition-opacity hover:opacity-100 opacity-0 md:opacity-100">
                <div>
                    <h1 className="font-bold text-white text-lg">
                        Chapter {manifest.chapterNumber}
                    </h1>
                    <div className="text-xs text-gray-400 font-mono">
                        {resolvedHash?.substring(0, 8)} • {manifest.files.length} pages
                    </div>
                </div>

                <div className="flex items-center space-x-4">
                    {/* Report peer button */}
                    <button
                        type="button"
                        onClick={() => setReportPeerModalOpen(true)}
                        title="Report a peer delivering wrong or switched content"
                        className="flex items-center gap-1.5 text-sm font-medium text-gray-400 hover:text-orange-400 transition-colors"
                    >
                        <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
                        </svg>
                        <span className="hidden sm:inline">Report Peer</span>
                    </button>

                    {/* Flag button */}
                    <button
                        type="button"
                        onClick={() => setFlagModalOpen(true)}
                        title={isLocallyFlagged ? 'You reported an issue with this chapter' : 'Flag this chapter'}
                        className={`flex items-center gap-1.5 text-sm font-medium transition-colors ${
                            isLocallyFlagged
                                ? 'text-red-400'
                                : 'text-gray-400 hover:text-red-400'
                        }`}
                    >
                        <svg className="w-4 h-4" viewBox="0 0 24 24">
                            <line x1="5" y1="21" x2="5" y2="3" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
                            <path
                                d="M5 3 L19 8 L5 13 Z"
                                fill={isLocallyFlagged ? 'currentColor' : 'none'}
                                stroke="currentColor"
                                strokeWidth="2"
                                strokeLinejoin="round"
                            />
                        </svg>
                        <span className="hidden sm:inline">{isLocallyFlagged ? 'Flagged' : 'Flag'}</span>
                    </button>

                    <Link
                        to={`/series/${seriesId}`}
                        className="text-sm font-medium text-gray-300 hover:text-white transition-colors"
                    >
                        Close
                    </Link>
                </div>
            </div>

            {/* Peer warning banner */}
            {peerWarning && !warningDismissed && (
                <div className="max-w-3xl mx-auto mt-4 px-4">
                    <div className="flex items-start gap-3 px-4 py-3 bg-amber-900/60 border border-amber-600/50 rounded-lg text-amber-200">
                        <svg className="w-5 h-5 text-amber-400 shrink-0 mt-0.5" fill="currentColor" viewBox="0 0 20 20">
                            <path fillRule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
                        </svg>
                        <div className="flex-1 text-sm">
                            <span className="font-semibold">Multiple peers report issues with this chapter.</span>
                            {' '}Content may be low quality, mislabeled, or incomplete.
                        </div>
                        <button
                            onClick={() => setWarningDismissed(true)}
                            className="text-amber-400 hover:text-amber-200 transition-colors shrink-0 ml-2"
                            aria-label="Dismiss"
                        >
                            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                            </svg>
                        </button>
                    </div>
                </div>
            )}

            {/* Pages Container */}
            <div className="max-w-3xl mx-auto space-y-2 py-4">
                {manifest.files.map((file, index) => (
                    <div key={file.hash} className="relative bg-gray-900 min-h-[50vh] flex items-center justify-center">
                        {/* /api/file/{hash} reads the PageManifest blob and reassembles the image from its chunks */}
                        <img
                            src={`/api/file/${file.hash}`}
                            alt={`Page ${index + 1}`}
                            className="w-full h-auto shadow-2xl"
                            loading="lazy"
                            onError={(e) => {
                                (e.target as HTMLImageElement).style.display = 'none';
                                (e.target as HTMLImageElement).parentElement!.innerHTML = `
                                    <div class="text-gray-500 p-8 text-center flex flex-col items-center">
                                        <span class="text-4xl mb-2">⚠️</span>
                                        <span>Failed to load page ${index + 1}</span>
                                    </div>
                                `;
                            }}
                        />
                    </div>
                ))}
            </div>

            {/* Footer Navigation Hints */}
            <div className="max-w-3xl mx-auto mt-8 px-4 text-center">
                <div className="inline-flex items-center gap-4 bg-gray-800 rounded-full px-6 py-3 shadow-lg">
                    <Link to={`/series/${seriesId}`} className="text-gray-300 hover:text-white transition-colors text-sm font-medium">
                        Return to Series
                    </Link>
                </div>
            </div>

            {/* Flag modal */}
            {flagModalOpen && resolvedHash && (
                <FlagModal
                    manifestHash={resolvedHash}
                    chapterLabel={chapterLabel}
                    onClose={() => setFlagModalOpen(false)}
                    onSubmitted={handleFlagSubmitted}
                />
            )}

            {/* Report peer modal */}
            {reportPeerModalOpen && resolvedHash && (
                <ReportPeerModal
                    manifestHash={resolvedHash}
                    chapterLabel={chapterLabel}
                    defaultNodeId={manifest?.deliveredByNodeId}
                    onClose={() => setReportPeerModalOpen(false)}
                    onSubmitted={() => setReportPeerModalOpen(false)}
                />
            )}
        </div>
    );
}
