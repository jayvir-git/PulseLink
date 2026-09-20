import { Link } from 'react-router-dom';

export function LandingPage() {
  return (
    <div className="landing">
      <header className="landing-nav">
        <Link className="brand" to="/"><span className="brand-mark" aria-hidden="true" /><strong>PulseLink</strong></Link>
        <nav aria-label="Main navigation"><a href="#workflow">How it works</a><Link className="button" to="/login">Sign in</Link></nav>
      </header>
      <main>
        <section className="hero">
          <div>
            <p className="eyebrow">FROM THE FIELD TO THE RECEIVING TEAM</p>
            <h1>One handoff.<br /><em>Every detail connected.</em></h1>
            <p className="hero-copy">Give the next care team the whole picture. Capture field observations, follow transport progress, and bring the clinical record together at arrival.</p>
            <div className="hero-actions"><Link className="button" to="/login">Open your workspace <span aria-hidden="true">↗</span></Link><a href="#workflow">Explore the workflow ↓</a></div>
            <p className="demo-note">Demonstration environment · Use fictional patient information only.</p>
          </div>
          <div className="handoff-preview" aria-label="Illustrative handoff preview">
            <div className="preview-top"><span className="eyebrow">THE HANDOFF RECORD</span><span className="preview-demo">Illustration</span></div>
            <div className="preview-route"><span>EMS team</span><span aria-hidden="true">→</span><span>Receiving hospital</span></div>
            <h2>Context that travels<br />with the patient.</h2>
            <ol className="preview-steps"><li><span>01</span><div><strong>Field observations</strong><small>Vitals, interventions, and clinical notes</small></div></li><li><span>02</span><div><strong>Transport progress</strong><small>A clear path from draft to arrival</small></div></li><li><span>03</span><div><strong>Structured handoff</strong><small>A shared record for the receiving team</small></div></li></ol>
            <div className="preview-footer"><span className="pulse-dot" /> Connected through every stage</div>
          </div>
        </section>
        <section className="workflow-section" id="workflow">
          <p className="eyebrow">A CLEAR PATH THROUGH CARE</p><h2>Built around the handoff.</h2>
          <div className="feature-grid">
            <article><span className="feature-number">01 / CAPTURE</span><h3>Start in the field</h3><p>Create a patient care report and record observations and interventions as the incident develops.</p></article>
            <article><span className="feature-number">02 / COORDINATE</span><h3>Follow the journey</h3><p>Advance through defined transport stages, with agency and hospital views tailored to each team.</p></article>
            <article><span className="feature-number">03 / HAND OFF</span><h3>Keep the record together</h3><p>Review the clinical summary, trace recorded activity, and export a structured, FHIR-inspired JSON bundle.</p></article>
          </div>
        </section>
      </main>
      <footer className="landing-footer"><strong>PulseLink</strong><span>EMS → hospital care continuity</span><Link to="/login">Sign in to your workspace →</Link></footer>
    </div>
  );
}
