import {
  ArrowDown,
  ArrowUpRight,
  Crosshair,
  Layers3,
  Radio,
  Smartphone,
  Users,
} from 'lucide-react';

export const dynamic = 'force-static';

const matchSteps = [
  ['01', 'Place the junction', 'The host scans the table and locks the miniature board in place.'],
  ['02', 'Pass the wheel', 'One driver takes control while everyone else watches from their own angle.'],
  ['03', 'Thread the gap', 'Build speed, read the traffic, and choose the point of no return.'],
  ['04', 'Share the wreck', 'Every screen sees the same chain reaction, score, and final result.'],
];

export default function Home() {
  return (
    <main>
      <section className="hero" id="top">
        <nav className="nav-shell" aria-label="Primary navigation">
          <a className="brand" href="#top" aria-label="Crashout home">
            <span className="brand-mark">C!</span>
            <span>Crashout</span>
          </a>
          <div className="nav-links">
            <a href="#game">The game</a>
            <a href="#build">Development</a>
            <a className="nav-cta" href="#playtest">
              Join playtests <ArrowUpRight aria-hidden="true" />
            </a>
          </div>
        </nav>

        <div className="hero-grid page-shell">
          <div className="hero-copy">
            <p className="eyebrow"><span /> Tabletop crash game for iPhone &amp; iPad</p>
            <h1>One driver.<br />One junction.<br /><em>Total chaos.</em></h1>
            <p className="hero-lede">
              Gather around the table, take the wheel, and turn one perfectly timed impact into a spectacular chain reaction everyone sees together.
            </p>
            <div className="hero-actions">
              <a className="button button-primary" href="#game">
                See how it plays <ArrowDown aria-hidden="true" />
              </a>
              <a className="button button-secondary" href="#build">
                Follow the build <ArrowUpRight aria-hidden="true" />
              </a>
            </div>
          </div>

          <div className="hero-art" aria-label="Crashout shared tabletop match concept">
            <div className="signal-pill"><Radio aria-hidden="true" /> Live match · 4 players</div>
            <div className="art-frame">
              {/* oxlint-disable-next-line next/no-img-element */}
              <img
                className="art-image"
                src="./og.png"
                alt="Four people viewing the same miniature junction pileup on their phones"
              />
              <div className="score-card">
                <span>Chain reaction</span>
                <strong>$184,750</strong>
                <small>7 vehicles · 3× cascade</small>
              </div>
            </div>
            <p className="art-caption"><span>01</span> The same crash, from every side of the table.</p>
          </div>
        </div>

        <div className="hero-footer page-shell">
          <span>Built for shared spectacle</span>
          <span className="hero-rule" />
          <span>Native AR · Local multiplayer · No accounts</span>
        </div>
      </section>

      <section className="manifesto" id="game">
        <div className="page-shell manifesto-grid">
          <p className="section-label">The premise</p>
          <div>
            <h2>A living board game with a very bad intersection.</h2>
            <p>
              Crashout turns the table into a miniature city junction. The active player drives into moving traffic while friends watch the exact same simulation from their own independently movable view.
            </p>
          </div>
        </div>
      </section>

      <section className="principles">
        <div className="page-shell principle-grid">
          <article>
            <span className="icon-box"><Crosshair aria-hidden="true" /></span>
            <p>01 / Skill</p>
            <h3>Pick the point of impact.</h3>
            <span>Speed, steering, timing, and traffic reading decide whether you clip one car or wake the whole junction.</span>
          </article>
          <article>
            <span className="icon-box"><Users aria-hidden="true" /></span>
            <p>02 / Together</p>
            <h3>Everyone gets a front-row seat.</h3>
            <span>Two to four players share the same tabletop world. One drives; everyone watches the pileup unfold live.</span>
          </article>
          <article>
            <span className="icon-box"><Layers3 aria-hidden="true" /></span>
            <p>03 / One world</p>
            <h3>Change the view, not the outcome.</h3>
            <span>Switch between AR tabletop, chase, and first-person cameras without resetting or duplicating the action.</span>
          </article>
        </div>
      </section>

      <section className="match" aria-labelledby="match-title">
        <div className="page-shell">
          <div className="match-heading">
            <p className="section-label">A round of Crashout</p>
            <h2 id="match-title">Study it. Send it.<br /><em>Watch it unfold.</em></h2>
          </div>
          <ol className="match-steps">
            {matchSteps.map(([number, title, body]) => (
              <li key={number}>
                <span>{number}</span>
                <div><h3>{title}</h3><p>{body}</p></div>
              </li>
            ))}
          </ol>
        </div>
      </section>

      <section className="build" id="build">
        <div className="page-shell build-grid">
          <div>
            <p className="section-label">Now in development</p>
            <h2>Native where it matters.</h2>
            <p className="build-copy">
              Crashout is being built in Unity for iPhone and iPad so the tabletop is the real, running game—not a disconnected browser preview. The first proof of concept is headed to TestFlight.
            </p>
          </div>
          <div className="build-card">
            <div className="build-card-top">
              <Smartphone aria-hidden="true" />
              <span>Prototype roadmap</span>
            </div>
            <ul>
              <li className="done"><span /> Shared game-world architecture <b>Defined</b></li>
              <li className="active"><span /> Single-device crash loop <b>In progress</b></li>
              <li><span /> Native AR tabletop <b>Next</b></li>
              <li><span /> 2–4 player LAN matches <b>Planned</b></li>
            </ul>
          </div>
        </div>
      </section>

      <section className="playtest" id="playtest">
        <div className="page-shell playtest-inner">
          <p className="section-label">Get the first build</p>
          <h2>Bring the table.<br /><em>We’ll bring the traffic.</em></h2>
          <p>Crashout is not publicly available yet. TestFlight playtest details will appear here when the native build is ready.</p>
          <div className="platform-line"><span>iPhone</span><i /> <span>iPad</span><i /> <span>2–4 players</span></div>
        </div>
      </section>

      <footer>
        <div className="page-shell footer-inner">
          <a className="brand" href="#top"><span className="brand-mark">C!</span><span>Crashout</span></a>
          <p>Original tabletop chaos. Built for the room you&apos;re in.</p>
          <span>© 2026 Crashout</span>
        </div>
      </footer>
    </main>
  );
}
