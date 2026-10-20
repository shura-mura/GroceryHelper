import { useEffect, useState } from 'react';

export function App() {
  const [status, setStatus] = useState('Connecting to backend');

  useEffect(() => {
    const controller = new AbortController();
    void fetch('/health', { signal: controller.signal })
      .then(async (response) => {
        if (!response.ok || await response.text() !== 'Healthy') {
          throw new Error('Backend probe failed');
        }
        setStatus('Backend connected');
      })
      .catch(() => {
        if (!controller.signal.aborted) setStatus('Backend unavailable');
      });
    return () => controller.abort();
  }, []);

  return (
    <main>
      <h1>GroceryHelper</h1>
      <p>Development scaffold. Application features are not implemented yet.</p>
      <p role="status">{status}</p>
    </main>
  );
}
