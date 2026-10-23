import type { DragEvent } from "react";
import { canMove, type StatusInfo } from "../statuses";
import type { ApplicationStatus, Job } from "../types";
import JobCard from "./JobCard";

interface ColumnProps {
  info: StatusInfo;
  jobs: Job[];
  /** The card being dragged right now, or null. */
  draggingJob: Job | null;
  onOpenJob: (job: Job) => void;
  onDragStart: (job: Job) => void;
  onDragEnd: () => void;
  onDropJob: (job: Job) => void;
  onMoveJob: (job: Job, target: ApplicationStatus) => void;
}

export default function Column({ info, jobs, draggingJob, onOpenJob, onDragStart, onDragEnd, onDropJob, onMoveJob }: ColumnProps) {
  // While a card is dragged, columns it may move to light up and all others fade.
  const dropState = draggingJob === null ? "idle" : canMove(draggingJob.status, info.value) ? "valid" : "invalid";

  function handleDragOver(event: DragEvent) {
    // Calling preventDefault is what tells the browser "dropping here is allowed".
    if (dropState === "valid") event.preventDefault();
  }

  function handleDrop(event: DragEvent) {
    event.preventDefault();
    if (draggingJob && dropState === "valid") onDropJob(draggingJob);
  }

  return (
    <section
      className="column"
      data-status={info.value}
      data-drop={dropState}
      onDragOver={handleDragOver}
      onDrop={handleDrop}
      aria-label={`${info.label}, ${jobs.length} jobs`}
    >
      <header className="column-header">
        <span className="status-dot" aria-hidden="true" />
        <h2>{info.label}</h2>
        <span className="column-count">{jobs.length}</span>
      </header>

      <div className="column-body">
        {jobs.length === 0 && <p className="column-empty">{info.emptyHint}</p>}
        {jobs.map((job) => (
          <JobCard
            key={job.id}
            job={job}
            isDragging={draggingJob?.id === job.id}
            onOpen={() => onOpenJob(job)}
            onDragStart={() => onDragStart(job)}
            onDragEnd={onDragEnd}
            onMove={(target) => onMoveJob(job, target)}
          />
        ))}
      </div>
    </section>
  );
}
