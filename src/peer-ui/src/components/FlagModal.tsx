import { useState } from 'react';
import { submitFlag } from '../api/flags';
import type { FlagCategory } from '../types/api';

const FLAG_CATEGORIES: { id: FlagCategory; label: string; description: string }[] = [
    { id: 'quality_low',       label: 'Low Quality',       description: 'Blurry, watermarked, or cropped images' },
    { id: 'page_order',        label: 'Wrong Page Order',  description: 'Pages are out of sequence' },
    { id: 'missing_pages',     label: 'Missing Pages',     description: 'Chapter appears incomplete' },
    { id: 'wrong_chapter',     label: 'Wrong Chapter',     description: 'Content does not match chapter number' },
    { id: 'duplicate',         label: 'Duplicate',         description: 'Same content already exists under another entry' },
    { id: 'nsfw',              label: 'NSFW',              description: 'Contains explicit or inappropriate content' },
    { id: 'malicious_content', label: 'Malicious Content', description: 'Suspicious or harmful content' },
    { id: 'bad_title',         label: 'Bad Title',         description: 'Title is incorrect, misleading, or missing' },
];

interface FlagModalProps {
    manifestHash: string;
    chapterLabel: string;
    onClose: () => void;
    onSubmitted: (manifestHash: string) => void;
}

export default function FlagModal({ manifestHash, chapterLabel, onClose, onSubmitted }: FlagModalProps) {
    const [selected, setSelected] = useState<Set<FlagCategory>>(new Set());
    const [comment, setComment] = useState('');
    const [submitting, setSubmitting] = useState(false);
    const [submitted, setSubmitted] = useState(false);

    function toggle(id: FlagCategory) {
        setSelected(prev => {
            const next = new Set(prev);
            if (next.has(id)) next.delete(id);
            else next.add(id);
            return next;
        });
    }

    async function handleSubmit() {
        if (selected.size === 0) return;
        setSubmitting(true);
        try {
            await submitFlag({
                manifestHash,
                categories: Array.from(selected),
                comment: comment.trim() || undefined,
            });
        } catch {
            // Best-effort — local flag state is still recorded by parent
        } finally {
            setSubmitting(false);
            setSubmitted(true);
            onSubmitted(manifestHash);
        }
    }

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" role="dialog" aria-modal="true">
            <div className="absolute inset-0 bg-black/50 backdrop-blur-sm" onClick={onClose} />
            <div className="relative bg-white rounded-xl shadow-2xl w-full max-w-md overflow-hidden">

                {submitted ? (
                    <div className="p-10 text-center">
                        <div className="w-14 h-14 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-4">
                            <svg className="w-7 h-7 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                            </svg>
                        </div>
                        <h3 className="text-lg font-bold text-gray-900 mb-1">Report Submitted</h3>
                        <p className="text-sm text-gray-500 mb-6">Thank you for helping keep the network clean.</p>
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
                                <h2 className="text-base font-bold text-gray-900">Flag Chapter</h2>
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

                        {/* Categories */}
                        <div className="px-5 pt-4 pb-2 max-h-72 overflow-y-auto">
                            <p className="text-[10px] font-semibold text-gray-400 uppercase tracking-wider mb-3">
                                Select all that apply
                            </p>
                            <div className="space-y-1.5">
                                {FLAG_CATEGORIES.map(cat => (
                                    <label
                                        key={cat.id}
                                        className={`flex items-start gap-3 px-3 py-2.5 rounded-lg cursor-pointer border transition-all ${
                                            selected.has(cat.id)
                                                ? 'border-red-300 bg-red-50'
                                                : 'border-transparent hover:border-gray-200 hover:bg-gray-50'
                                        }`}
                                    >
                                        <input
                                            type="checkbox"
                                            checked={selected.has(cat.id)}
                                            onChange={() => toggle(cat.id)}
                                            className="mt-0.5 accent-red-500 w-4 h-4 shrink-0"
                                        />
                                        <div className="min-w-0">
                                            <div className="text-sm font-medium text-gray-800 leading-tight">{cat.label}</div>
                                            <div className="text-xs text-gray-500 leading-tight mt-0.5">{cat.description}</div>
                                        </div>
                                    </label>
                                ))}
                            </div>
                        </div>

                        {/* Comment */}
                        <div className="px-5 pt-3 pb-4">
                            <label className="text-[10px] font-semibold text-gray-400 uppercase tracking-wider block mb-2">
                                Additional comments{' '}
                                <span className="normal-case font-normal">(optional)</span>
                            </label>
                            <textarea
                                value={comment}
                                onChange={e => setComment(e.target.value)}
                                placeholder="Describe the issue in more detail..."
                                rows={3}
                                maxLength={500}
                                className="w-full text-sm border border-gray-200 rounded-lg px-3 py-2 text-gray-700 placeholder-gray-400 resize-none focus:outline-none focus:ring-2 focus:ring-red-300 focus:border-transparent transition"
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
                                disabled={selected.size === 0 || submitting}
                                className="flex items-center gap-2 px-4 py-2 bg-red-600 text-white rounded-lg text-sm font-semibold hover:bg-red-700 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                            >
                                {submitting && (
                                    <svg className="w-3.5 h-3.5 animate-spin" fill="none" viewBox="0 0 24 24">
                                        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                                        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                                    </svg>
                                )}
                                Submit Report
                            </button>
                        </div>
                    </>
                )}
            </div>
        </div>
    );
}
