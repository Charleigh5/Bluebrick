import "./styles.css";

export function App() {
  return (
    <main className="assistant-shell">
      <header className="assistant-header">
        <strong>BlueBrick Assistant</strong>
      </header>
      <section className="assistant-thread" aria-live="polite" />
    </main>
  );
}
