import { apiFetch } from './client';

export interface Settings {
    isFullSeeder: boolean;
}

export async function getSettings(): Promise<Settings> {
    const response = await apiFetch('/api/settings');
    if (!response.ok) throw new Error('Failed to fetch settings');
    return await response.json();
}

export async function updateSettings(settings: Partial<Settings>): Promise<void> {
    const response = await apiFetch('/api/settings', {
        method: 'PUT',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(settings)
    });
    if (!response.ok) throw new Error('Failed to update settings');
}
