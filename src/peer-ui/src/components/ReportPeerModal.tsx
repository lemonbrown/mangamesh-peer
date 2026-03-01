import { useState, useEffect } from 'react';
import { submitFlag } from '../api/flags';
import { getManifestSeeders } from '../api/peers';
import type { ManifestSeeder } from '../types/api';

interface ReportPeerModalProps {
    manifestHash: string;
    chapterLabel: string;
    /** Node ID of the peer that delivered the chapter — pre-populates the report. */
    defaultNodeId?: string;
    onClose: () => void;
    onSubmitted: () => void;
}

export default function ReportPeerModal({ manifestHash, chapterLabel, defaultNodeId, onClose, onSubmitted }: ReportPeerModalProps) {
    const [seeders, setSeeders] = useState<ManifestSeeder[]>([]);
    const [loadingSeeders, setLoadingSeeders] = useState(!defaultNodeId);
    const [selectedNodeId, setSelectedNodeId] = useState(defaultNodeId ?? '');
    const [comment, setComment] = useState('');
    const [submitting, setSubmitting] = useState(false);
    const [submitted, setSubmitted] = useState(false);

    useEffect(() => {
        // Only fetch seeder list if we don't already know which peer delivered the content
        if (defaultNodeId) return;
        getManifestSeeders(manifestHash)
            .then(setSeeders)
            .finally(() => setLoadingSeeders(false));
    }, [manifestHash, defaultNodeId]);

    async function handleSubmit() {
        if (!selectedNodeId) return;
        setSubmitting(true);
        try {
            await submitFlag({
                manifestHash,
                categories: ['wrong_chapter'],
                comment: comment.trim() || undefined,
                reportedNodeId: selectedNodeId,
            });
        } catch {
            // best-effort
        } finally {
            setSubmitting(false);
            setSubmitted(true);
            onSubmitted();
        }
    }

    function truncateNodeId(id: string): string {
        return id.length > 16 ? `${id.slice(0, 8)}…${id.slice(-8)}` : id;
    }

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" role="dialog" aria-modal="true">
            <div className="absolute inset-0 bg-black/50 backdrop-blur-sm" onClick={onClose} />
            <div className="relative bg-white rounded-xl shadow-2xl w-full max-w-md overflow-hidden">

                {submitted ? (
                    <div className="p-10 text-center">
                        <div className="w-14 h-14 bg-orange-100 rounded-full flex items-center justify-center mx-auto mb-4">
                            <svg className="w-7 h-7 text-orange-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                            </svg>
                        </div>
                        <h3 className="text-lg font-bold text-gray-900 mb-1">Report Submitted</h3>
                        <p className="text-sm text-gray-500 mb-6">Thank you. This peer has been reported for review.</p>
                        <button
                            onClick={onClose}
                            className="px-5 py-2 bg-gray-900 text-white rounded-lg text-sm font-semibold hover:bg-gray-700 transition-colors"
                        >
                            Close
                        </button>
                    </div>
                ) : (
                    <>
                        {/* Header */}
                        <div className="flex items-start justify-between px-5 pt-5 pb-4 border-b border-gray-100">
                            <div>
                                <h2 className="text-base font-bold text-gray-900">Report Peer</h2>
                                <p className="text-xs text-gray-500 mt-0.5 truncate max-w-xs">{chapterLabel}</p>
                            </div>
                            <button
                                onClick={onClose}
                                className="text-gray-400 hover:text-gray-600 transition-colors ml-4 shrink-0"
                                aria-label="Close"
                            >
                                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </button>
                        </div>

                        <div className="px-5 pt-4 pb-2">
                            {defaultNodeId ? (
                                /* Delivering peer is known — show pre-selected card */
                                <>
                                    <p className="text-sm text-gray-600 mb-3">
                                        Report the peer that delivered this chapter's content.
                                    </p>
                                    <p className="text-[10px] font-semibold text-gray-400 uppercase tracking-wider mb-2">
                                        Delivering peer
                                    </p>
                                    <div className="flex items-center gap-3 px-3 py-2.5 rounded-lg border border-orange-300 bg-orange-50 mb-3">
                                        <svg className="w-4 h-4 text-orange-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 12h14M12 5l7 7-7 7" />
                                        </svg>
                                        <div className="min-w-0 flex-1">
                                            <div className="text-xs font-mono text-gray-800 truncate">{truncateNodeId(defaultNodeId)}</div>
                                            <div className="text-[10px] text-gray-400 mt-0.5">Sent this chapter to your node</div>
                                        </div>
                                    </div>
                                    {/* Allow overriding the node ID if needed */}
                                    <div>
                                        <label className="text-[10px] font-semibold text-gray-400 uppercase tracking-wider block mb-1">
                                            Report a different node ID instead
                                        </label>
                                        <input
                                            type="text"
                                            value={selectedNodeId === defaultNodeId ? '' : selectedNodeId}
                                            onChange={e => setSelectedNodeId(e.target.value || defaultNodeId)}
                                            placeholder="Paste node ID to override…"
                                            className="w-full text-sm font-mono border border-gray-200 rounded-lg px-3 py-2 text-gray-700 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-orange-300 focus:border-transparent transition"
                                        />
                                    </div>
                                </>
                            ) : (
                                /* Delivering peer unknown — let user pick from seeders or enter manually */
                                <>
                                    <p className="text-sm text-gray-600 mb-4">
                                        Select the peer that delivered wrong or switched content for this chapter.
                                    </p>

                                    <p className="text-[10px] font-semibold text-gray-400 uppercase tracking-wider mb-2">
                                        Known seeders for this manifest
                                    </p>

                                    {loadingSeeders ? (
                                        <div className="flex items-center gap-2 py-4 text-sm text-gray-400">
                                            <svg className="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                                                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                                                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                                            </svg>
                                            Looking up seeders…
                                        </div>
                                    ) : seeders.length === 0 ? (
                                        <p className="text-sm text-gray-400 italic py-2">
                                            No known seeders found. Enter a node ID manually below.
                                        </p>
                                    ) : (
                                        <div className="space-y-1.5 max-h-48 overflow-y-auto mb-3">
                                            {seeders.map(seeder => (
                                                <label
                                                    key={seeder.nodeId}
                                                    className={`flex items-center gap-3 px-3 py-2.5 rounded-lg cursor-pointer border transition-all ${
                                                        selectedNodeId === seeder.nodeId
                                                            ? 'border-orange-300 bg-orange-50'
                                                            : 'border-transparent hover:border-gray-200 hover:bg-gray-50'
                                                    }`}
                                                >
                                                    <input
                                                        type="radio"
                                                        name="seeder"
                                                        value={seeder.nodeId}
                                                        checked={selectedNodeId === seeder.nodeId}
                                                        onChange={() => setSelectedNodeId(seeder.nodeId)}
                                                        className="accent-orange-500 w-4 h-4 shrink-0"
                                                    />
                                                    <div className="min-w-0 flex-1">
                                                        <div className="text-xs font-mono text-gray-800 truncate">
                                                            {truncateNodeId(seeder.nodeId)}
                                                        </div>
                                                        <div className="text-[10px] text-gray-400 mt-0.5">
                                                            Last seen {new Date(seeder.lastSeen).toLocaleString()}
                                                        </div>
                                                    </div>
                                                </label>
                                            ))}
                                        </div>
                                    )}

                                    <div className="mt-2">
                                        <label className="text-[10px] font-semibold text-gray-400 uppercase tracking-wider block mb-1">
                                            {seeders.length > 0 ? 'Or enter node ID manually' : 'Node ID'}
                                        </label>
                                        <input
                                            type="text"
                                            value={seeders.some(s => s.nodeId === selectedNodeId) ? '' : selectedNodeId}
                                            onChange={e => setSelectedNodeId(e.target.value)}
                                            placeholder="Paste node ID…"
                                            className="w-full text-sm font-mono border border-gray-200 rounded-lg px-3 py-2 text-gray-700 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-orange-300 focus:border-transparent transition"
                                        />
                                    </div>
                                </>
                            )}
                        </div>

                        {/* Comment */}
                        <div className="px-5 pt-3 pb-4">
                            <label className="text-[10px] font-semibold text-gray-400 uppercase tracking-wider block mb-2">
                                Details <span className="normal-case font-normal">(optional)</span>
                            </label>
                            <textarea
                                value={comment}
                                onChange={e => setComment(e.target.value)}
                                placeholder="Describe what was wrong with the delivered content…"
                                rows={2}
                                maxLength={500}
                                className="w-full text-sm border border-gray-200 rounded-lg px-3 py-2 text-gray-700 placeholder-gray-400 resize-none focus:outline-none focus:ring-2 focus:ring-orange-300 focus:border-transparent transition"
                            />
                            <div className="text-right text-[10px] text-gray-400 mt-0.5">{comment.length}/500</div>
                        </div>

                        {/* Footer */}
                        <div className="flex items-center justify-between px-5 py-3 bg-gray-50 border-t border-gray-100">
                            <button
                                onClick={onClose}
                                className="text-sm text-gray-500 hover:text-gray-700 transition-colors font-medium"
                            >
                                Cancel
                            </button>
                            <button
                                onClick={handleSubmit}
                                disabled={!selectedNodeId.trim() || submitting}
                                className="flex items-center gap-2 px-4 py-2 bg-orange-600 text-white rounded-lg text-sm font-semibold hover:bg-orange-700 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                            >
                                {submitting && (
                                    <svg className="w-3.5 h-3.5 animate-spin" fill="none" viewBox="0 0 24 24">
                                        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                                        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                                    </svg>
                                )}
                                Report Peer
                            </button>
                        </div>
                    </>
                )}
            </div>
        </div>
    );
}
