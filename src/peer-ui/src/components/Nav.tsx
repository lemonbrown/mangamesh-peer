import { Link, useLocation } from 'react-router-dom';
import { useState, useEffect, useRef } from 'react';
import classNames from 'classnames';
import NodeStatusIndicator from './NodeStatusIndicator';

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
                    <div className="flex space-x-1">
                        <NavLink to="/series">
                            {/* Book icon */}
                            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253" />
                            </svg>
                            Series
                        </NavLink>
                        <NavLink to="/subscriptions">
                            {/* Bookmark icon */}
                            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 5a2 2 0 012-2h10a2 2 0 012 2v16l-7-3.5L5 21V5z" />
                            </svg>
                            Subscriptions
                        </NavLink>
                        <NavLink to="/import">
                            {/* Upload icon */}
                            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
                            </svg>
                            Publish
                        </NavLink>
                    </div>
                </div>

                <div className="flex items-center gap-3">
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
                </nav>
            </div>
        </>
    );
}
