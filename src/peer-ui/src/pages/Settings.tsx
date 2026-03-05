import { useState, useEffect } from 'react';
import { getSettings, updateSettings } from '../api/settings';
import type { Settings } from '../api/settings';

export default function SettingsPage() {
    const [settings, setSettings] = useState<Settings | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [saving, setSaving] = useState(false);

    useEffect(() => {
        getSettings()
            .then(setSettings)
            .catch(e => setError(e instanceof Error ? e.message : 'Failed to load settings'))
            .finally(() => setLoading(false));
    }, []);

    const handleToggleFullSeeder = async () => {
        if (!settings) return;
        setSaving(true);
        try {
            const nextValue = !settings.isFullSeeder;
            await updateSettings({ isFullSeeder: nextValue });
            setSettings({ ...settings, isFullSeeder: nextValue });
        } catch (e) {
            alert('Failed to update settings');
        } finally {
            setSaving(false);
        }
    };

    if (loading) return <div className="p-8 text-gray-500">Loading settings...</div>;
    if (error) return <div className="p-8 text-red-500">{error}</div>;

    return (
        <div className="max-w-4xl mx-auto space-y-8 pb-12">
            <div>
                <h1 className="text-3xl font-bold text-gray-900 mb-2">Settings</h1>
                <p className="text-gray-500">Manage your node configuration.</p>
            </div>

            <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
                <div className="p-6 border-b border-gray-100 pb-5">
                    <h2 className="text-lg font-semibold text-gray-900 mb-1">Replication & Seeding</h2>
                    <p className="text-sm text-gray-500">
                        Control how this node participates in the network.
                    </p>
                </div>

                <div className="p-6">
                    <div className="flex items-start justify-between">
                        <div className="pr-8">
                            <h3 className="text-base font-medium text-gray-900">Global Full Seeder</h3>
                            <p className="mt-1 text-sm text-gray-500">
                                When enabled, this node will attempt to download and keep all chapters for every series it encounters on the network.
                                This will use significantly more storage and bandwidth.
                            </p>
                        </div>
                        <button
                            type="button"
                            onClick={handleToggleFullSeeder}
                            disabled={saving}
                            className={`relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 ${settings?.isFullSeeder ? 'bg-blue-600' : 'bg-gray-200'
                                } ${saving ? 'opacity-50 cursor-not-allowed' : ''}`}
                            role="switch"
                            aria-checked={settings?.isFullSeeder}
                        >
                            <span
                                aria-hidden="true"
                                className={`pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${settings?.isFullSeeder ? 'translate-x-5' : 'translate-x-0'
                                    }`}
                            />
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}
