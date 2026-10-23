import { useEffect, useState } from "react";
import { labelOf } from "../statuses";
import type { Job } from "../types";

interface JobDetailPanelProps {
  job: Job;
  onClose: () => void;
  onEdit: () => void;
  onSaveNotes: (notes: string) => Promise<void>;
  onDelete: () => Promise<void>;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric" });
}

export default function JobDetailPanel({ job, onClose, onEdit, onSaveNotes, onDelete }: JobDetailPanelProps) {
  const [notes, setNotes] = useState(job.notes ?? "");
  const [savingNotes, setSavingNotes] = useState(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);

  // Show the saved notes again when another job is opened in the panel.
  useEffect(() => {
    setNotes(job.notes ?? "");
    setConfirmingDelete(false);
  }, [job.id, job.notes]);

  useEffect(() => {
    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === "Escape") onClose();
    }
    window.addEventListener("keydown", closeOnEscape);
    return () => window.removeEventListener("keydown", closeOnEscape);
  }, [onClose]);

  async function handleSaveNotes() {
    setSavingNotes(true);
    await onSaveNotes(notes);
    setSavingNotes(false);
  }

  const notesChanged = notes.trim() !== (job.notes ?? "");

  return (
    <>
      <div className="overlay" onClick={onClose} aria-hidden="true" />
      <aside className="panel" role="dialog" aria-label={`Details for ${job.title}`}>
        <header className="panel-header">
          <div>
            <span className="pill" data-status={job.status}>
              <span className="status-dot" aria-hidden="true" />
              {labelOf(job.status)}
            </span>
            <h2>{job.title}</h2>
            <p className="panel-company">
              {job.company}
              {job.location ? ` · ${job.location}` : ""}
            </p>
          </div>
          <button className="icon-button" onClick={onClose} aria-label="Close details">
            ×
          </button>
        </header>

        <div className="panel-body">
          <dl className="facts">
            <dt>Saved</dt>
            <dd>{formatDate(job.createdAt)}</dd>
            {job.appliedAt && (
              <>
                <dt>Applied</dt>
                <dd>{formatDate(job.appliedAt)}</dd>
              </>
            )}
            {job.url && (
              <>
                <dt>Ad</dt>
                <dd>
                  <a href={job.url} target="_blank" rel="noreferrer noopener">
                    Open the original ad
                  </a>
                </dd>
              </>
            )}
          </dl>

          <h3>Your notes</h3>
          <textarea
            className="notes"
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            maxLength={5000}
            rows={4}
            placeholder="Contacts, deadlines, questions to ask…"
          />
          <button className="button" onClick={() => void handleSaveNotes()} disabled={!notesChanged || savingNotes}>
            {savingNotes ? "Saving…" : "Save notes"}
          </button>

          <h3>Job description</h3>
          <p className="description">{job.description}</p>
        </div>

        <footer className="panel-footer">
          <button className="button" onClick={onEdit}>
            Edit
          </button>
          {confirmingDelete ? (
            <span className="confirm">
              Delete this job?
              <button className="button button-danger" onClick={() => void onDelete()}>
                Yes, delete
              </button>
              <button className="button" onClick={() => setConfirmingDelete(false)}>
                No
              </button>
            </span>
          ) : (
            <button className="button button-quiet-danger" onClick={() => setConfirmingDelete(true)}>
              Delete
            </button>
          )}
        </footer>
      </aside>
    </>
  );
}
