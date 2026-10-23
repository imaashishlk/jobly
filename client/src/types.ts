// These types mirror the JSON the API sends (JobResponse in Jobly.Application). Dates arrive as ISO text.

export type ApplicationStatus =
  | "Saved"
  | "PlanningToApply"
  | "Applied"
  | "Interview"
  | "Offer"
  | "Rejected"
  | "Withdrawn"
  | "Ghosted";

export interface Job {
  id: string;
  title: string;
  company: string;
  location: string | null;
  description: string;
  url: string | null;
  source: "Manual" | "Arbeidsplassen";
  externalId: string | null;
  status: ApplicationStatus;
  notes: string | null;
  appliedAt: string | null;
  createdAt: string;
  updatedAt: string;
}

/** The user's CV as plain text. There is one for the whole app. */
export interface Cv {
  id: string;
  text: string;
  fileName: string | null;
  source: "Uploaded" | "Pasted";
  characterCount: number;
  updatedAt: string;
}

/** What the Match agent found out about one job. */
export interface JobMatch {
  jobId: string;
  score: number;
  summary: string;
  matchingSkills: string[];
  gaps: string[];
  model: string;
  createdAt: string;
}

/** The fields the user types into the add/edit form. */
export interface JobInput {
  title: string;
  company: string;
  location: string;
  url: string;
  description: string;
}
