import type { ApplicationStatus } from "./types";

export interface StatusInfo {
  value: ApplicationStatus;
  label: string;
  /** Short text shown in an empty column. */
  emptyHint: string;
}

// The order here is the order of the columns on the board.
export const STATUSES: StatusInfo[] = [
  { value: "Saved", label: "Saved", emptyHint: "Jobs you want to look at later." },
  { value: "PlanningToApply", label: "Planning to apply", emptyHint: "Drag a saved job here when you decide to apply." },
  { value: "Applied", label: "Applied", emptyHint: "Jobs you have sent an application for." },
  { value: "Interview", label: "Interview", emptyHint: "Good news goes here." },
  { value: "Offer", label: "Offer", emptyHint: "An offer. Congratulations!" },
  { value: "Rejected", label: "Rejected", emptyHint: "No is also an answer." },
  { value: "Withdrawn", label: "Withdrawn", emptyHint: "Applications you stopped yourself." },
  { value: "Ghosted", label: "Ghosted", emptyHint: "Applied or interviewed, but no reply." },
];

// The same rules as JobApplication.ChangeStatus in Jobly.Domain. The client uses this table only to
// light up valid columns while dragging. The API still checks every move and answers 409 if it is not allowed.
const ALLOWED_MOVES: Record<ApplicationStatus, ApplicationStatus[]> = {
  Saved: ["PlanningToApply", "Applied", "Withdrawn"],
  PlanningToApply: ["Applied", "Withdrawn"],
  Applied: ["Interview", "Rejected", "Withdrawn", "Ghosted"],
  Interview: ["Offer", "Rejected", "Withdrawn", "Ghosted"],
  Offer: [],
  Rejected: [],
  Withdrawn: [],
  Ghosted: [],
};

export function allowedMovesFrom(status: ApplicationStatus): ApplicationStatus[] {
  return ALLOWED_MOVES[status];
}

export function canMove(from: ApplicationStatus, to: ApplicationStatus): boolean {
  return ALLOWED_MOVES[from].includes(to);
}

/** A final column: the card can't be moved anywhere else. */
export function isFinal(status: ApplicationStatus): boolean {
  return ALLOWED_MOVES[status].length === 0;
}

export function labelOf(status: ApplicationStatus): string {
  return STATUSES.find((info) => info.value === status)?.label ?? status;
}
