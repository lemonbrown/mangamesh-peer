import Nav from './Nav';
import { Outlet } from 'react-router-dom';
import { useNode } from '../NodeContext';

const harnessUrl = import.meta.env.VITE_TEST_HARNESS_URL as string | undefined;

export default function Layout() {
    const { testNodeUrl } = useNode();
    const nodeReady = !harnessUrl || !!testNodeUrl;

    return (
        <div className="min-h-screen bg-gray-50 font-sans text-gray-900">
            <Nav />
            <main className="max-w-7xl mx-auto py-8 px-6">
                {nodeReady ? (
                    <div key={testNodeUrl}>
                        <Outlet />
                    </div>
                ) : (
                    <div className="flex items-center justify-center py-24 text-gray-400 text-sm">
                        Connecting to test node…
                    </div>
                )}
            </main>
        </div>
    );
}
