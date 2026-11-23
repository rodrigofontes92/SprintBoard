import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'

type User = { id: string; email: string }
type Project = { id: string; name: string; description: string | null; createdAt: string }
type AuthMode = 'login' | 'register'

const requestAntiforgeryToken = async () => {
  const response = await fetch('/api/auth/antiforgery', { credentials: 'include' })
  if (!response.ok) throw new Error('The API could not issue an antiforgery token.')
  const body = await response.json() as { requestToken?: string }
  if (!body.requestToken) throw new Error('The API returned an empty antiforgery token.')
  return body.requestToken
}

const errorMessage = async (response: Response) => {
  const body = await response.json().catch(() => null) as
    | { error?: string; title?: string; detail?: string; errors?: Record<string, string[]> }
    | null

  if (body?.errors) {
    return Object.values(body.errors).flat().join(' ')
  }

  return body?.error ?? body?.detail ?? body?.title ?? `Request failed (${response.status}).`
}

export default function App() {
  const [user, setUser] = useState<User | null>(null)
  const [projects, setProjects] = useState<Project[]>([])
  const [antiforgeryToken, setAntiforgeryToken] = useState('')
  const [mode, setMode] = useState<AuthMode>('login')
  const [loading, setLoading] = useState(true)
  const [working, setWorking] = useState(false)
  const [message, setMessage] = useState('')

  useEffect(() => {
    const initialize = async () => {
      setAntiforgeryToken(await requestAntiforgeryToken())

      const userResponse = await fetch('/api/auth/me', { credentials: 'include' })
      if (userResponse.ok) {
        const signedInUser = await userResponse.json() as User
        setUser(signedInUser)
        await loadProjects()
      } else if (userResponse.status !== 401) {
        throw new Error(await errorMessage(userResponse))
      }
    }

    void initialize()
      .catch((error: unknown) => setMessage(error instanceof Error ? error.message : 'The application could not start.'))
      .finally(() => setLoading(false))
  }, [])

  const loadProjects = async () => {
    const response = await fetch('/api/projects', { credentials: 'include' })
    if (!response.ok) throw new Error(await errorMessage(response))
    setProjects(await response.json() as Project[])
  }

  const submitAuthentication = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setMessage('')
    setWorking(true)

    const formElement = event.currentTarget
    const form = new FormData(formElement)
    const payload = {
      email: String(form.get('email') ?? ''),
      password: String(form.get('password') ?? ''),
    }

    try {
      const response = await fetch(`/api/auth/${mode}`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': antiforgeryToken },
        body: JSON.stringify(payload),
      })
      if (!response.ok) throw new Error(await errorMessage(response))

      setUser(await response.json() as User)
      setAntiforgeryToken(await requestAntiforgeryToken())
      await loadProjects()
      formElement.reset()
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Authentication failed.')
    } finally {
      setWorking(false)
    }
  }

  const createProject = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setMessage('')
    setWorking(true)

    const formElement = event.currentTarget
    const form = new FormData(formElement)
    const payload = {
      name: String(form.get('name') ?? ''),
      description: String(form.get('description') ?? ''),
    }

    try {
      const response = await fetch('/api/projects', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': antiforgeryToken },
        body: JSON.stringify(payload),
      })
      if (!response.ok) throw new Error(await errorMessage(response))

      const createdProject = await response.json() as Project
      setProjects(current => [...current, createdProject].sort((left, right) => left.name.localeCompare(right.name)))
      formElement.reset()
      setMessage('Project created.')
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'The project could not be created.')
    } finally {
      setWorking(false)
    }
  }

  const signOut = async () => {
    setMessage('')
    setWorking(true)
    try {
      const response = await fetch('/api/auth/logout', {
        method: 'POST',
        credentials: 'include',
        headers: { 'X-CSRF-TOKEN': antiforgeryToken },
      })
      if (!response.ok) throw new Error(await errorMessage(response))
      setAntiforgeryToken(await requestAntiforgeryToken())
      setUser(null)
      setProjects([])
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Sign out failed.')
    } finally {
      setWorking(false)
    }
  }

  if (loading) {
    return <main className="loading-state">Connecting to SprintBoard…</main>
  }

  return (
    <main className="app-shell">
      <header className="topbar">
        <a className="brand" href="/" aria-label="SprintBoard home">
          <span className="brand-mark">S</span>
          <span>SprintBoard</span>
        </a>
        {user && (
          <div className="account-bar">
            <span>{user.email}</span>
            <button className="button button-quiet" onClick={signOut} disabled={working}>Sign out</button>
          </div>
        )}
      </header>

      {message && <div className="notice" role="status">{message}</div>}

      {!user ? (
        <section className="auth-layout">
          <div className="intro-panel">
            <p className="eyebrow">A clearer way to move work forward</p>
            <h1>Make good work easier to see.</h1>
            <p className="intro-copy">Keep projects together, give each next step a home, and make progress visible to your team.</p>
            <div className="intro-stats"><span>Projects</span><span>Tasks</span><span>Momentum</span></div>
          </div>

          <section className="auth-card" aria-labelledby="auth-title">
            <div className="tab-row" role="tablist" aria-label="Account access">
              <button className={mode === 'login' ? 'tab active' : 'tab'} onClick={() => setMode('login')} role="tab" aria-selected={mode === 'login'}>Sign in</button>
              <button className={mode === 'register' ? 'tab active' : 'tab'} onClick={() => setMode('register')} role="tab" aria-selected={mode === 'register'}>Create account</button>
            </div>
            <h2 id="auth-title">{mode === 'login' ? 'Welcome back' : 'Start your workspace'}</h2>
            <p className="muted">{mode === 'login' ? 'Sign in to see your projects.' : 'Create an account to start organising your work.'}</p>
            <form onSubmit={submitAuthentication} className="form-stack">
              <label>Email address<input name="email" type="email" autoComplete="email" required maxLength={254} /></label>
              <label>Password<input name="password" type="password" autoComplete={mode === 'login' ? 'current-password' : 'new-password'} required minLength={mode === 'login' ? 1 : 12} maxLength={128} /></label>
              {mode === 'register' && <p className="field-hint">Use at least 12 characters, including uppercase, lowercase, a number, and a symbol.</p>}
              <button className="button button-primary button-wide" type="submit" disabled={working || !antiforgeryToken}>
                {working ? 'Please wait…' : mode === 'login' ? 'Sign in' : 'Create account'}
              </button>
            </form>
          </section>
        </section>
      ) : (
        <section className="workspace">
          <div className="workspace-heading">
            <div><p className="eyebrow">Your workspace</p><h1>Projects</h1><p className="muted">A focused place for the work you own.</p></div>
            <span className="count-pill">{projects.length} {projects.length === 1 ? 'project' : 'projects'}</span>
          </div>

          <div className="workspace-grid">
            <section className="project-list" aria-label="Your projects">
              {projects.length === 0 ? (
                <div className="empty-state"><span className="empty-icon">＋</span><h2>Your first project starts here</h2><p>Create a project to give a goal and its future tasks a shared home.</p></div>
              ) : projects.map(project => (
                <article className="project-card" key={project.id}>
                  <span className="project-card-icon">{project.name.slice(0, 1).toUpperCase()}</span>
                  <div><h2>{project.name}</h2><p>{project.description || 'No description yet.'}</p></div>
                  <time dateTime={project.createdAt}>{new Date(project.createdAt).toLocaleDateString()}</time>
                </article>
              ))}
            </section>

            <section className="create-card">
              <p className="eyebrow">Get started</p><h2>Create a project</h2><p className="muted">Only you can see this project for now.</p>
              <form onSubmit={createProject} className="form-stack">
                <label>Project name<input name="name" required minLength={1} maxLength={120} placeholder="e.g. Website refresh" /></label>
                <label>Description <span className="optional">Optional</span><textarea name="description" rows={4} maxLength={2000} placeholder="What are you working towards?" /></label>
                <button className="button button-primary" type="submit" disabled={working || !antiforgeryToken}>{working ? 'Saving…' : 'Create project'}</button>
              </form>
            </section>
          </div>
        </section>
      )}

      <footer className="footer"><span>SprintBoard</span><span>Small steps. Shared progress.</span></footer>
    </main>
  )
}
