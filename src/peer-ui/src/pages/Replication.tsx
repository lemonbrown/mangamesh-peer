import { useEffect, useState } from 'react';
import { getReplicationOverview } from '../api/replication';
import type { ReplicationSeries, ReplicationChapter } from '../api/replication';
import LangFlag from '../components/LangFlag';

function fmtBytes(b: number): string {
    if (b === 0) return '0 B';
    if (b < 1024) return `${b} B`;
    if (b < 1024 * 1024) return `${(b / 1024).toFixed(1)} KB`;
    return `${(b / 1024 / 1024).toFixed(2)} MB`;
}

function ReplicaBadge({ estimate, minimum }: { estimate: number; minimum: number }) {
    const healthy = estimate >= minimum && minimum > 0;
    const noData = estimate === 0;
    return (
        <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold ${
            noData
                ? 'bg-gray-100 text-gray-400'
                : healthy
                    ? 'bg-green-100 text-green-700'
                    : 'bg-amber-100 text-amber-700'
        }`}>
            <svg className="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                    d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
            {noData ? '?' : `${estimate}`} replica{estimate !== 1 ? 's' : ''}
        </span>
    );
}

function CoverageBar({ local, total, label }: { local: number; total: number; label: string }) {
    const pct = total > 0 ? Math.min(100, (local / total) * 100) : 0;
    const full = pct >= 100;
    return (
        <div className="flex items-center gap-2 min-w-0">
            <div className="flex-1 h-1.5 bg-gray-100 rounded-full overflow-hidden" style={{ minWidth: '4rem' }}>
                <div
                    className={`h-full rounded-full transition-all ${full ? 'bg-green-500' : 'bg-blue-400'}`}
                    style={{ width: `${pct}%` }}
                />
            </div>
            <span className="text-[11px] text-gray-500 whitespace-nowrap shrink-0">{label}</span>
        </div>
    );
}

function ChapterRow({ ch }: { ch: ReplicationChapter }) {
    const chunkPct = ch.totalChunks > 0 ? Math.round((ch.localChunks / ch.totalChunks) * 100) : 0;
    const byteLabel = `${fmtBytes(ch.localBytes)} / ${fmtBytes(ch.totalBytes)}`;
    const chunkLabel = `${ch.localChunks} / ${ch.totalChunks} chunks`;

    return (
        <div className="px-4 py-3 flex flex-wrap items-center gap-3 text-sm">
            {/* Chapter number + title */}
            <div className="shrink-0 w-16 text-right">
                <span className="font-semibold text-gray-800 text-xs">Ch.&nbsp;{
                    Number.isInteger(ch.chapterNumber) ? ch.chapterNumber : ch.chapterNumber.toFixed(1)
                }</span>
            </div>

            {/* Badges */}
            <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded bg-blue-50 text-blue-700 text-[10px] font-medium shrink-0">
                <LangFlag code={ch.language} /> {ch.language?.toUpperCase()}
            </span>

            {ch.isDownloaded ? (
                <span className="px-1.5 py-0.5 rounded bg-green-50 text-green-700 text-[10px] font-medium shrink-0">Seeding</span>
            ) : (
                <span className="px-1.5 py-0.5 rounded bg-purple-50 text-purple-700 text-[10px] font-medium shrink-0">Replica</span>
            )}

            {ch.scanGroup && (
                <span className="text-xs text-gray-500 shrink-0">{ch.scanGroup}</span>
            )}

            {/* Coverage bar — bytes */}
            <div className="flex-1 min-w-[8rem]">
                <CoverageBar local={ch.localBytes} total={ch.totalBytes} label={byteLabel} />
            </div>

            {/* Chunk count */}
            <span className="text-[11px] text-gray-400 shrink-0">{chunkLabel} ({chunkPct}%)</span>

            {/* Replica estimate */}
            <ReplicaBadge estimate={ch.replicaEstimate} minimum={ch.minimumReplicas} />

            {/* Rare chunk warning */}
            {ch.rareChunkCount > 0 && (
                <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded bg-red-50 text-red-600 text-[10px] font-medium">
                    <svg className="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                            d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
                    </svg>
                    {ch.rareChunkCount} rare chunk{ch.rareChunkCount !== 1 ? 's' : ''}
                </span>
            )}
        </div>
    );
}

function SeriesCard({ s }: { s: ReplicationSeries }) {
    const [expanded, setExpanded] = useState(true);

    const totalBytes = s.chapters.reduce((acc, c) => acc + c.totalBytes, 0);
    const localBytes = s.chapters.reduce((acc, c) => acc + c.localBytes, 0);
    const coveragePct = totalBytes > 0 ? Math.round((localBytes / totalBytes) * 100) : 0;
    const title = s.seriesTitle || s.seriesId;

    return (
        <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
            {/* Series header */}
            <button
                className="w-full flex items-center gap-4 px-5 py-4 text-left hover:bg-gray-50 transition-colors"
                onClick={() => setExpanded(e => !e)}
            >
                {/* Cover thumbnail */}
                <div className="shrink-0 w-10 h-14 rounded overflow-hidden bg-gray-100 relative flex items-center justify-center">
                    <svg className="w-5 h-5 text-gray-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5}
                            d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253" />
                    </svg>
                    {s.externalMangaId && (
                        <img
                            src={`/covers/${s.externalMangaId}.card.webp`}
                            alt=""
                            className="absolute inset-0 w-full h-full object-cover"
                            onError={e => { (e.target as HTMLImageElement).style.display = 'none'; }}
                        />
                    )}
                </div>

                <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                        <span className="font-semibold text-gray-900 truncate">{title}</span>
                        <span className="text-xs text-gray-400">{s.chapters.length} chapter{s.chapters.length !== 1 ? 's' : ''}</span>
                    </div>
                    {/* Series-level coverage bar */}
                    <div className="mt-1.5 flex items-center gap-2">
                        <div className="w-32 h-1.5 bg-gray-100 rounded-full overflow-hidden">
                            <div
                                className={`h-full rounded-full ${coveragePct >= 100 ? 'bg-green-500' : 'bg-blue-400'}`}
                                style={{ width: `${coveragePct}%` }}
                            />
                        </div>
                        <span className="text-[11px] text-gray-500">{fmtBytes(localBytes)} / {fmtBytes(totalBytes)}</span>
                        <ReplicaBadge estimate={s.replicaEstimate} minimum={
                            s.chapters.length > 0 ? (s.chapters[0]?.minimumReplicas ?? 1) : 1
                        } />
                    </div>
                </div>

                <svg
                    className={`w-4 h-4 text-gray-400 shrink-0 transition-transform ${expanded ? 'rotate-180' : ''}`}
                    fill="none" viewBox="0 0 24 24" stroke="currentColor"
                >
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                </svg>
            </button>

            {/* Chapter list */}
            {expanded && (
                <div className="border-t border-gray-100 divide-y divide-gray-50">
                    {s.chapters.map(ch => (
                        <ChapterRow key={ch.manifestHash} ch={ch} />
                    ))}
                </div>
            )}
        </div>
    );
}

export default function Replication() {
    const [overview, setOverview] = useState<{ series: ReplicationSeries[] } | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    async function load() {
        setLoading(true);
        setError(null);
        try {
            setOverview(await getReplicationOverview());
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Failed to load replication data');
        } finally {
            setLoading(false);
        }
    }

    useEffect(() => { load(); }, []);

    const totalSeries = overview?.series.length ?? 0;
    const totalChapters = overview?.series.reduce((a, s) => a + s.chapters.length, 0) ?? 0;
    const seeding = overview?.series.flatMap(s => s.chapters).filter(c => c.isDownloaded).length ?? 0;
    const replicating = totalChapters - seeding;

    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between">
                <h1 className="text-2xl font-bold text-gray-900">Replication</h1>
                <button
                    onClick={load}
                    disabled={loading}
                    className="px-3 py-1.5 text-sm rounded-md bg-gray-100 hover:bg-gray-200 text-gray-700 disabled:opacity-50 transition-colors"
                >
                    {loading ? 'Loading…' : 'Refresh'}
                </button>
            </div>

            {/* Summary stats */}
            {overview && (
                <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
                    {[
                        { label: 'Series', value: totalSeries },
                        { label: 'Total chapters', value: totalChapters },
                        { label: 'Seeding', value: seeding },
                        { label: 'Replicating', value: replicating },
                    ].map(({ label, value }) => (
                        <div key={label} className="bg-white rounded-lg border border-gray-200 px-4 py-3">
                            <div className="text-xs text-gray-500">{label}</div>
                            <div className="text-2xl font-mono font-bold text-gray-900 mt-0.5">{value}</div>
                        </div>
                    ))}
                </div>
            )}

            {error && (
                <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg px-4 py-3 text-sm">{error}</div>
            )}

            {loading && !overview && (
                <div className="text-center text-gray-400 py-12 text-sm">Loading replication data…</div>
            )}

            {overview && overview.series.length === 0 && (
                <div className="bg-white rounded-lg border border-gray-200 p-8 text-center text-gray-500 text-sm">
                    No chapters stored locally yet.
                </div>
            )}

            <div className="space-y-3">
                {overview?.series.map(s => (
                    <SeriesCard key={s.seriesId} s={s} />
                ))}
            </div>
        </div>
    );
}
