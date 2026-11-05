import { IS_DEMO } from './config';
import { resetDemo } from './store';

// The disclosure is part of the demo, not decoration around it: a reader has to
// be able to tell at a glance that no server is involved in what they are seeing.
export function DemoBanner() {
  if (!IS_DEMO) return null;

  return (
    <aside className="demo-banner" aria-label="Demo environment">
      <span>
        <strong>Interactive demo</strong> · Fictional patients. Browser-only data.
      </span>
      <span className="demo-banner-actions">
        <details><summary>About this demo</summary><p>No server or database is connected. What you enter stays in this browser. The API’s rules are verified separately against SQLite and SQL Server. <a href="https://github.com/jayvir-git/PulseLink/tree/main/backend/PulseLink.Tests" target="_blank" rel="noreferrer">See the tests</a></p></details>
        <button
          type="button"
          className="ghost"
          onClick={() => {
            resetDemo();
            window.location.assign('/');
          }}
        >
          Reset demo data
        </button>
      </span>
    </aside>
  );
}
