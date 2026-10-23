import { useCallback, useEffect, useMemo, useState } from "react";
import { api, ApiError } from "./api";
import Column from "./components/Column";
import JobDetailPanel from "./components/JobDetailPanel";
import JobFormDialog from "./components/JobFormDialog";
import Toasts, { type Toast } from "./components/Toasts";
import { canMove, labelOf, STATUSES } from "./statuses";
import type { ApplicationStatus, Job, JobInput } from "./types";

// The form is closed (null), adding a new job, or editing an existing one.
type FormState = { mode: "create" } | { mode: "edit"; job: Job } | null;

let nextToastId = 1;

function messageOf(error: unknown): string {
  return error instanceof ApiError ? error.message : "Something went wrong. Please try again.";
}

export default function App() {
  const [jobs, setJobs] = useState<Job[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [form, setForm] = useState<FormState>(null);
  const [draggingId, setDraggingId] = useState<string | null>(null);
  const [toasts, setToasts] = useState<Toast[]>([]);

  const notify = useCallback((kind: Toast["kind"], text: string) => {
    const id = nextToastId++;
    setToasts((current) => [...current, { id, kind, text }]);
    // Toasts remove themselves after a few seconds.
    window.setTimeout(() => setToasts((current) => current.filter((toast) => toast.id !== id)), 5000);
  }, []);

  const loadJobs = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      setJobs(await api.listJobs());
    } catch (error) {
      setLoadError(messageOf(error));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadJobs();
  }, [loadJobs]);

  const replaceJob = useCallback((job: Job) => {
    setJobs((current) => current.map((existing) => (existing.id === job.id ? job : existing)));
  }, []);

  const jobsByStatus = useMemo(() => {
    const groups = new Map<ApplicationStatus, Job[]>(STATUSES.map((info) => [info.value, []]));
    for (const job of jobs) groups.get(job.status)?.push(job);
    return groups;
  }, [jobs]);

  const selectedJob = jobs.find((job) => job.id === selectedId) ?? null;
  const draggingJob = jobs.find((job) => job.id === draggingId) ?? null;

  async function moveJob(job: Job, target: ApplicationStatus) {
    if (job.status === target) return;
    if (!canMove(job.status, target)) {
      notify("error", `A job cannot move from ${labelOf(job.status)} to ${labelOf(target)}.`);
      return;
    }

    // Optimistic update: the card jumps to the new column at once. If the API says no, it jumps back.
    replaceJob({ ...job, status: target });
    try {
      replaceJob(await api.changeStatus(job.id, target));
      notify("success", `Moved to ${labelOf(target)}.`);
    } catch (error) {
      replaceJob(job);
      notify("error", messageOf(error));
    }
  }

  // Returns an error message for the form to show, or null when the job was saved.
  async function submitForm(input: JobInput): Promise<string | null> {
    try {
      switch (form?.mode) {
        case "edit": {
          replaceJob(await api.updateJob(form.job.id, input));
          notify("success", "Job updated.");
          break;
        }
        default: {
          const created = await api.createJob(input);
          setJobs((current) => [created, ...current]);
          notify("success", "Job saved.");
          break;
        }
      }
      setForm(null);
      return null;
    } catch (error) {
      return messageOf(error);
    }
  }

  async function saveNotes(job: Job, notes: string) {
    try {
      replaceJob(await api.updateNotes(job.id, notes));
      notify("success", "Notes saved.");
    } catch (error) {
      notify("error", messageOf(error));
    }
  }

  async function deleteJob(job: Job) {
    try {
      await api.deleteJob(job.id);
      setJobs((current) => current.filter((existing) => existing.id !== job.id));
      setSelectedId(null);
      notify("success", "Job deleted.");
    } catch (error) {
      notify("error", messageOf(error));
    }
  }

  return (
    <div className="app">
      <header className="app-header">
        <div>
          <h1>Jobly</h1>
          <p className="tagline">Track every application from first look to final answer.</p>
        </div>
        <button className="button button-primary" onClick={() => setForm({ mode: "create" })}>
          + Add job
        </button>
      </header>

      {loadError ? (
        <div className="state-message" role="alert">
          <p>{loadError}</p>
          <button className="button" onClick={() => void loadJobs()}>
            Try again
          </button>
        </div>
      ) : loading ? (
        <div className="state-message" role="status">
          Loading your jobs…
        </div>
      ) : (
        <main className="board" aria-label="Job board">
          {STATUSES.map((info) => (
            <Column
              key={info.value}
              info={info}
              jobs={jobsByStatus.get(info.value) ?? []}
              draggingJob={draggingJob}
              onOpenJob={(job) => setSelectedId(job.id)}
              onDragStart={(job) => setDraggingId(job.id)}
              onDragEnd={() => setDraggingId(null)}
              onDropJob={(job) => {
                setDraggingId(null);
                void moveJob(job, info.value);
              }}
              onMoveJob={(job, target) => void moveJob(job, target)}
            />
          ))}
        </main>
      )}

      {selectedJob && (
        <JobDetailPanel
          job={selectedJob}
          onClose={() => setSelectedId(null)}
          onEdit={() => setForm({ mode: "edit", job: selectedJob })}
          onSaveNotes={(notes) => saveNotes(selectedJob, notes)}
          onDelete={() => deleteJob(selectedJob)}
        />
      )}

      {form && (
        <JobFormDialog
          key={form.mode === "edit" ? form.job.id : "new"}
          job={form.mode === "edit" ? form.job : null}
          onSubmit={submitForm}
          onCancel={() => setForm(null)}
        />
      )}

      <Toasts toasts={toasts} />
    </div>
  );
}
