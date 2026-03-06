import React, { useEffect, useState } from 'react';
import { useNode } from '../NodeContext';

interface TestNode {
    id: string;
    apiUrl: string;
    dhtPort: number;
}

const NodeSelector: React.FC = () => {
    const { testNodeUrl, selectNode } = useNode();
    const [nodes, setNodes] = useState<TestNode[]>([]);
    const [isOpen, setIsOpen] = useState(false);

    // Check if we are in a test harness environment or have it enabled
    const harnessUrl = import.meta.env.VITE_TEST_HARNESS_URL || localStorage.getItem('testHarnessUrl');

    useEffect(() => {
        if (!harnessUrl) return;

        const fetchNodes = async () => {
            try {
                const response = await fetch(`${harnessUrl}/harness/nodes`);
                if (response.ok) {
                    const data = await response.json();
                    setNodes(data);
                }
            } catch (error) {
                console.error('Failed to fetch test harness nodes:', error);
            }
        };

        fetchNodes();
        const interval = setInterval(fetchNodes, 5000);
        return () => clearInterval(interval);
    }, [harnessUrl]);

    // If no URL is explicitly set yet, auto-select the first available node
    useEffect(() => {
        if (nodes.length > 0 && !testNodeUrl) {
            selectNode(nodes[0].apiUrl);
        }
    }, [nodes, testNodeUrl, selectNode]);

    if (!harnessUrl || nodes.length === 0) return null;

    const currentNode = nodes.find(n => n.apiUrl === testNodeUrl) || nodes[0];

    const handleSelectNode = (apiUrl: string) => {
        selectNode(apiUrl);
        setIsOpen(false);
    };

    return (
        <div className="relative">
            <button
                onClick={() => setIsOpen(!isOpen)}
                className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium rounded-md bg-indigo-100 text-indigo-700 hover:bg-indigo-200 border border-indigo-300 transition-colors"
            >
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19.428 15.428a2 2 0 00-1.022-.547l-2.387-.477a6 6 0 00-3.86.517l-.318.158a6 6 0 01-3.86.517L6.05 15.21a2 2 0 00-1.806.547M8 4h8l-1 1v5.172a2 2 0 00.586 1.414l5 5c1.26 1.26.367 3.414-1.415 3.414H4.828c-1.782 0-2.674-2.154-1.414-3.414l5-5A2 2 0 009 10.172V5L8 4z" />
                </svg>
                {currentNode ? `Test Mode: ${currentNode.id}` : 'Test Mode'}
            </button>

            {isOpen && (
                <div className="absolute right-0 top-full mt-2 w-56 bg-white rounded-md shadow-xl border border-gray-200 z-50 overflow-hidden">
                    <div className="px-3 py-2 bg-gray-50 border-b border-gray-100 text-xs font-semibold text-gray-500 uppercase">
                        Select Active Node
                    </div>
                    <div className="max-h-64 overflow-y-auto">
                        {nodes.map(node => (
                            <button
                                key={node.id}
                                onClick={() => handleSelectNode(node.apiUrl)}
                                className={`w-full text-left px-4 py-3 text-sm flex flex-col transition-colors ${node.apiUrl === testNodeUrl
                                        ? 'bg-indigo-50 border-l-4 border-indigo-500'
                                        : 'hover:bg-gray-50 border-l-4 border-transparent'
                                    }`}
                            >
                                <span className={node.apiUrl === testNodeUrl ? 'text-indigo-700 font-medium' : 'text-gray-700'}>
                                    {node.id}
                                </span>
                                <span className="text-xs text-gray-500 font-mono mt-1">
                                    {node.apiUrl}
                                </span>
                            </button>
                        ))}
                    </div>
                </div>
            )}

            {isOpen && (
                <div className="fixed inset-0 z-40" onClick={() => setIsOpen(false)} />
            )}
        </div>
    );
};

export default NodeSelector;
