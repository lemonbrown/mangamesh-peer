import { Link, useLocation } from 'react-router-dom';
import { useState, useEffect, useRef } from 'react';
import classNames from 'classnames';
import NodeStatusIndicator from './NodeStatusIndicator';
import NodeSelector from './NodeSelector';

const NavLink = ({ to, children }: { to: string; children: React.ReactNode }) => {
    const location = useLocation();
    const isActive = location.pathname === to || location.pathname.startsWith(to + '/');

    return (
        <Link
            to={to}
            className={classNames(
                'flex items-center gap-1.5 px-3 py-2 rounded-md text-sm font-medium transition-colors',
                {
                    'bg-gray-200 text-gray-900': isActive,
                    'text-gray-600 hover:bg-gray-100 hover:text-gray-900': !isActive,
                }
            )}
        >
            {children}
        </Link>
    );
};

const SidebarLink = ({ to, children, onClick }: { to: string; children: React.ReactNode; onClick: () => void }) => {
    const location = useLocation();
    const isActive = location.pathname === to;

    return (
        <Link
            to={to}
            onClick={onClick}
            className={classNames(
                'flex items-center gap-3 px-4 py-2.5 rounded-lg text-sm font-medium transition-colors',
                {
                    'bg-gray-100 text-gray-900': isActive,
                    'text-gray-600 hover:bg-gray-50 hover:text-gray-900': !isActive,
                }
            )}
        >
            {children}
        </Link>
    );
};

export default function Nav() {
    const [sidebarOpen, setSidebarOpen] = useState(false);
    const sidebarRef = useRef<HTMLDivElement>(null);

    // Close sidebar when clicking outside
    useEffect(() => {
        function handleClick(e: MouseEvent) {
            if (sidebarRef.current && !sidebarRef.current.contains(e.target as Node)) {
                setSidebarOpen(false);
            }
        }
        if (sidebarOpen) {
            document.addEventListener('mousedown', handleClick);
        }
        return () => document.removeEventListener('mousedown', handleClick);
    }, [sidebarOpen]);

    // Close sidebar on Escape
    useEffect(() => {
        function handleKey(e: KeyboardEvent) {
            if (e.key === 'Escape') setSidebarOpen(false);
        }
        document.addEventListener('keydown', handleKey);
        return () => document.removeEventListener('keydown', handleKey);
    }, []);

    return (
        <>
            <nav className="bg-white border-b border-gray-200 px-6 py-3 flex items-center justify-between">
                <div className="flex items-center space-x-8">
                    <div className="text-xl font-bold text-gray-800 tracking-tight">
                        MangaMesh
                    </div>
                    <div className="flex items-center gap-1">
                        <NavLink to="/peer">
                            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                            </svg>
                            Peer
                        </NavLink>
                        <NavLink to="/broadcasts">
                            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8.111 16.404a5.5 5.5 0 017.778 0M12 20h.01m-7.08-7.071c3.904-3.905 10.236-3.905 14.141 0M1.394 9.393c5.857-5.857 15.355-5.857 21.213 0" />
                            </svg>
                            Broadcasts
                        </NavLink>
                        <NavLink to="/import">
                            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
                            </svg>
                            Import
                        </NavLink>
                    </div>
                </div>

                <div className="flex items-center gap-3">
                    <NodeSelector />
                    <NodeStatusIndicator />

                    {/* Gear / Settings button */}
                    <button
                        onClick={() => setSidebarOpen(prev => !prev)}
                        className={classNames(
                            'p-2 rounded-md transition-colors',
                            sidebarOpen
                                ? 'bg-gray-200 text-gray-900'
                                : 'text-gray-500 hover:bg-gray-100 hover:text-gray-800'
                        )}
                        title="Settings"
                        aria-label="Open settings"
                    >
                        <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                        </svg>
                    </button>
                </div>
            </nav>

            {/* Backdrop */}
            {sidebarOpen && (
                <div className="fixed inset-0 z-20 bg-black/20" aria-hidden="true" />
            )}

            {/* Settings sidebar */}
            <div
                ref={sidebarRef}
                className={classNames(
                    'fixed top-0 right-0 h-full w-64 bg-white border-l border-gray-200 shadow-xl z-30 flex flex-col transition-transform duration-200',
                    sidebarOpen ? 'translate-x-0' : 'translate-x-full'
                )}
            >
                <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
                    <span className="text-sm font-semibold text-gray-700 uppercase tracking-wider">Settings</span>
                    <button
                        onClick={() => setSidebarOpen(false)}
                        className="p-1 rounded text-gray-400 hover:text-gray-700 hover:bg-gray-100 transition-colors"
                        aria-label="Close settings"
                    >
                        <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                        </svg>
                    </button>
                </div>

                <nav className="flex-1 px-3 py-4 space-y-1">
                    <SidebarLink to="/storage" onClick={() => setSidebarOpen(false)}>
                        {/* Database icon */}
                        <svg className="w-5 h-5 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 7v10c0 2.21 3.582 4 8 4s8-1.79 8-4V7M4 7c0 2.21 3.582 4 8 4s8-1.79 8-4M4 7c0-2.21 3.582-4 8-4s8 1.79 8 4m0 5c0 2.21-3.582 4-8 4s-8-1.79-8-4" />
                        </svg>
                        Storage
                    </SidebarLink>
                    <SidebarLink to="/replication" onClick={() => setSidebarOpen(false)}>
                        {/* Share / replicate icon */}
                        <svg className="w-5 h-5 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
                        </svg>
                        Replication
                    </SidebarLink>
                    <SidebarLink to="/keys" onClick={() => setSidebarOpen(false)}>
                        {/* Key icon */}
                        <svg className="w-5 h-5 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 7a2 2 0 012 2m4 0a6 6 0 01-7.743 5.743L11 17H9v2H7v2H4a1 1 0 01-1-1v-2.586a1 1 0 01.293-.707l5.964-5.964A6 6 0 1121 9z" />
                        </svg>
                        Keys
                    </SidebarLink>
                    <SidebarLink to="/logs" onClick={() => setSidebarOpen(false)}>
                        {/* Document/list icon */}
                        <svg className="w-5 h-5 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                        </svg>
                        Logs
                    </SidebarLink>
                    <SidebarLink to="/settings" onClick={() => setSidebarOpen(false)}>
                        {/* Cog icon */}
                        <svg className="w-5 h-5 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                        </svg>
                        Settings
                    </SidebarLink>
                </nav>
            </div>
        </>
    );
}
