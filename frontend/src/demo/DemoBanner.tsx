import { IS_DEMO } from './config';
import { resetDemo } from './store';

// The disclosure is part of the demo, not decoration around it: a reader has to
// be able to tell at a glance that no server is involved in what they are seeing.
export function DemoBanner() {
  if (!IS_DEMO) return null;

  return (
    <div className="demo-banner" role="status">
      <span>
        <strong>Demo:</strong> this runs entirely in your browser. There is no server and no
        database, and nothing you enter leaves this device. The same rules, including the status
        machine, version conflicts and idempotent retries, are implemented in C# and verified in CI
        against SQLite and SQL Server.
      </span>
      <span className="demo-banner-actions">
        <a href="https://github.com/jayvir-git/PulseLink/tree/main/backend/PulseLink.Tests" target="_blank" rel="noreferrer">
          See the tests
        </a>
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
    </div>
  );
}
