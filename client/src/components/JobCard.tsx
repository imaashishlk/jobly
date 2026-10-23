import type { DragEvent } from "react";
import { allowedMovesFrom, isFinal, labelOf } from "../statuses";
import type { ApplicationStatus, Job } from "../types";

interface JobCardProps {
  job: Job;
  isDragging: boolean;
  onOpen: () => void;
  onDragStart: () => void;
  onDragEnd: () => void;
  onMove: (target: ApplicationStatus) => void;
}

export default function JobCard({ job, isDragging, onOpen, onDragStart, onDragEnd, onMove }: JobCardProps) {
  const targets = allowedMovesFrom(job.status);
  // Cards in a final column (Offer, Rejected, Withdrawn, Ghosted) stay where they are.
  const canDrag = !isFinal(job.status);

  function handleDragStart(event: DragEvent) {
    // Firefox only starts a drag when some data is set.
    event.dataTransfer.setData("text/plain", job.id);
    event.dataTransfer.effectAllowed = "move";
    onDragStart();
  }

  return (
    <article
      className="card"
      data-dragging={isDragging}
      draggable={canDrag}
      onDragStart={canDrag ? handleDragStart : undefined}
      onDragEnd={onDragEnd}
    >
      {/* A real button, so the card can be opened with the keyboard too. */}
      <button className="card-open" onClick={onOpen}>
        <span className="card-title">{job.title}</span>
        <span className="card-company">{job.company}</span>
      </button>

      <div className="card-meta">
        {job.location && <span>{job.location}</span>}
        <span className="card-source">{job.source === "Manual" ? "Added by you" : "Arbeidsplassen"}</span>
      </div>

      {/* The drop-down is the way to move a card without dragging (keyboard, touch screens). */}
      {targets.length > 0 && (
        <select
          className="card-move"
          aria-label={`Move ${job.title} to another column`}
          value=""
          onChange={(event) => onMove(event.target.value as ApplicationStatus)}
        >
          <option value="" disabled>
            Move to…
          </option>
          {targets.map((target) => (
            <option key={target} value={target}>
              {labelOf(target)}
            </option>
          ))}
        </select>
      )}
    </article>
  );
}
