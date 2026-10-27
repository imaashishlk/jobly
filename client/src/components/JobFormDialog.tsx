import { useEffect, useRef, useState, type FormEvent } from "react";
import type { Job, JobInput } from "../types";

interface JobFormDialogProps {
  /** The job being edited, or null when adding a new one. */
  job: Job | null;
  /** Resolves to an error message to show, or null when saved (the parent then closes the form). */
  onSubmit: (input: JobInput) => Promise<string | null>;
  onCancel: () => void;
}

export default function JobFormDialog({ job, onSubmit, onCancel }: JobFormDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const [input, setInput] = useState<JobInput>({
    title: job?.title ?? "",
    company: job?.company ?? "",
    location: job?.location ?? "",
    url: job?.url ?? "",
    description: job?.description ?? "",
  });
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  // showModal() gives us the dark backdrop, focus handling and the Escape key for free.
  useEffect(() => {
    dialogRef.current?.showModal();
  }, []);

  function setField(field: keyof JobInput, value: string) {
    setInput((current) => ({ ...current, [field]: value }));
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setSaving(true);
    setError(null);
    const message = await onSubmit(input);
    // On success the parent removes this dialog, so only a failure needs to update the screen.
    if (message !== null) {
      setError(message);
      setSaving(false);
    }
  }

  return (
    <dialog ref={dialogRef} className="dialog" onClose={onCancel} aria-labelledby="job-form-title">
      <form onSubmit={handleSubmit}>
        <h2 id="job-form-title">{job ? "Edit job" : "Add a job"}</h2>
        <p className="dialog-hint">Paste the job description from Arbeidsplassen or any other site.</p>

        <label>
          Title
          <input value={input.title} onChange={(e) => setField("title", e.target.value)} maxLength={200} required autoFocus />
        </label>

        <label>
          Company
          <input value={input.company} onChange={(e) => setField("company", e.target.value)} maxLength={200} required />
        </label>

        <div className="form-row">
          <label>
            Location
            <input value={input.location} onChange={(e) => setField("location", e.target.value)} maxLength={200} />
          </label>
          <label>
            Link to the ad
            <input type="url" value={input.url} onChange={(e) => setField("url", e.target.value)} maxLength={2000} placeholder="https://" />
          </label>
        </div>

        <label>
          Description
          <textarea
            value={input.description}
            onChange={(e) => setField("description", e.target.value)}
            maxLength={20000}
            rows={10}
            required
          />
        </label>

        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}

        <div className="dialog-actions">
          <button type="button" className="button" onClick={() => dialogRef.current?.close()}>
            Cancel
          </button>
          <button type="submit" className="button button-primary" disabled={saving}>
            {saving ? "Saving…" : "Save"}
          </button>
        </div>
      </form>
    </dialog>
  );
}
