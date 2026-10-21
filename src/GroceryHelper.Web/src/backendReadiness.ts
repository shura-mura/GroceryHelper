export async function checkBackendReadiness(signal: AbortSignal): Promise<void> {
  if (!import.meta.env.DEV && import.meta.env.MODE !== 'testing') return;

  const response = await fetch('/health', { signal });
  if (!response.ok || await response.text() !== 'Healthy') {
    throw new Error('Backend probe failed');
  }
}
