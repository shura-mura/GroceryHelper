import { type FormEvent, useEffect, useState } from 'react';
import {
  initializeAuthentication,
  logOutEverywhere as requestLogOutEverywhere,
  signIn as requestSignIn,
} from './authentication';
import { checkBackendReadiness } from './backendReadiness';

export function App() {
  const [status, setStatus] = useState('Connecting to backend');
  const [csrfToken, setCsrfToken] = useState<string>();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [account, setAccount] = useState<string>();
  const [error, setError] = useState<string>();
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    const controller = new AbortController();

    void checkBackendReadiness(controller.signal)
      .then(() => initializeAuthentication(controller.signal))
      .then(({ token, session, sessionEnded }) => {
        setCsrfToken(token);
        setAccount(session?.username);
        if (sessionEnded) {
          setError('Your session ended. Unsaved changes were discarded.');
        }
        setStatus('Backend connected');
      })
      .catch(() => {
        if (!controller.signal.aborted) setStatus('Backend unavailable');
      });

    return () => controller.abort();
  }, []);

  async function signIn(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!csrfToken) return;

    setSubmitting(true);
    setError(undefined);
    try {
      const result = await requestSignIn(username, password, csrfToken);
      if (!result.authenticated) {
        setError(result.message);
        return;
      }

      setPassword('');
      setAccount(result.session.username);
    } catch {
      setError('Unable to sign in');
    } finally {
      setSubmitting(false);
    }
  }

  async function logOutEverywhere() {
    setSubmitting(true);
    setError(undefined);
    try {
      await requestLogOutEverywhere();
      setAccount(undefined);
      setUsername('');
      setPassword('');
    } catch {
      setError('Unable to log out');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main>
      <h1>GroceryHelper</h1>
      <p role="status">{status}</p>
      {account
        ? (
          <section>
            <h2>Signed in as {account}</h2>
            <button
              disabled={submitting}
              type="button"
              onClick={() => { void logOutEverywhere(); }}
            >
              Log out everywhere
            </button>
            {error && <p role="alert">{error}</p>}
          </section>
        )
        : (
          <form onSubmit={(event) => { void signIn(event); }}>
            <h2>Sign in</h2>
            <label>
              Username
              <input
                autoComplete="username"
                value={username}
                onChange={(event) => setUsername(event.target.value)}
              />
            </label>
            <label>
              Password
              <input
                autoComplete="current-password"
                type="password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
              />
            </label>
            <button disabled={!csrfToken || submitting} type="submit">
              {submitting ? 'Signing in' : 'Sign in'}
            </button>
            {error && <p role="alert">{error}</p>}
          </form>
        )}
    </main>
  );
}
