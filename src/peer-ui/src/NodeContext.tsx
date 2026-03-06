import { createContext, useCallback, useContext, useState } from 'react';

interface NodeContextValue {
    testNodeUrl: string;
    selectNode: (url: string) => void;
}

export const NodeContext = createContext<NodeContextValue>({
    testNodeUrl: '',
    selectNode: () => {},
});

export function NodeProvider({ children }: { children: React.ReactNode }) {
    const [testNodeUrl, setTestNodeUrl] = useState(
        () => localStorage.getItem('testNodeUrl') || ''
    );

    const selectNode = useCallback((url: string) => {
        localStorage.setItem('testNodeUrl', url);
        setTestNodeUrl(url);
    }, []);

    return (
        <NodeContext.Provider value={{ testNodeUrl, selectNode }}>
            {children}
        </NodeContext.Provider>
    );
}

export const useNode = () => useContext(NodeContext);
