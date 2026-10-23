import { useEffect, useState } from "react";
import type { JobMatch } from "../types";

interface MatchSectionProps {
  match: JobMatch | null;
  analyzing: boolean;
  error: string | null;
  /** True or false once known, null while the app is still checking. */
  hasCv: boolean | null;
  onAnalyze: () => void;
  onOpenCv: () => void;
}

// Counts the seconds while an analysis runs, so a slow model does not look like a frozen page.
function useElapsedSeconds(active: boolean): number {
  const [seconds, setSeconds] = useState(0);

  useEffect(() => {
    if (!active) return;
    setSeconds(0);
    const timer = window.setInterval(() => setSeconds((current) => current + 1), 1000);
    return () => window.clearInterval(timer);
  }, [active]);

  return seconds;
}

function levelOf(score: number): "high" | "medium" | "low" {
  if (score >= 70) return "high";
  if (score >= 40) return "medium";
  return "low";
}

export default function MatchSection({ match, analyzing, error, hasCv, onAnalyze, onOpenCv }: MatchSectionProps) {
  const seconds = useElapsedSeconds(analyzing);

  return (
    <section className="match" aria-label="Match with my CV">
      <h3>Match with my CV</h3>

      {hasCv === false && (
        <p className="match-hint">
          Add your CV first, then the language model can compare it with this job.{" "}
          <button type="button" className="link-button" onClick={onOpenCv}>
            Add your CV
          </button>
        </p>
      )}

      {analyzing && (
        <p className="match-progress" role="status">
          <span className="spinner" aria-hidden="true" />
          Analyzing… {seconds} s. This computer runs the model on the CPU, so it can take a few minutes.
        </p>
      )}

      {error && !analyzing && (
        <p className="form-error" role="alert">
          {error}
        </p>
      )}

      {match && (
        <div className="match-result" data-level={levelOf(match.score)}>
          <div className="score">
            <span className="score-number">{match.score}</span>
            <span className="score-out-of">/ 100</span>
            <div
              className="score-bar"
              role="meter"
              aria-label="Match score"
              aria-valuemin={0}
              aria-valuemax={100}
              aria-valuenow={match.score}
            >
              <div className="score-fill" style={{ width: `${match.score}%` }} />
            </div>
          </div>

          <p className="match-summary">{match.summary}</p>

          {match.matchingSkills.length > 0 && (
            <>
              <h4>Matches</h4>
              <ul className="chips">
                {match.matchingSkills.map((skill) => (
                  <li key={skill}>{skill}</li>
                ))}
              </ul>
            </>
          )}

          {match.gaps.length > 0 && (
            <>
              <h4>Gaps</h4>
              <ul className="gap-list">
                {match.gaps.map((gap) => (
                  <li key={gap}>{gap}</li>
                ))}
              </ul>
            </>
          )}

          <p className="match-meta">
            By {match.model} on {new Date(match.createdAt).toLocaleDateString()}. A guide to help you decide, not a verdict.
          </p>
        </div>
      )}

      <button type="button" className="button button-primary" disabled={analyzing || hasCv === false} onClick={onAnalyze}>
        {analyzing ? "Analyzing…" : match ? "Analyze again" : "Analyze this job"}
      </button>
    </section>
  );
}
