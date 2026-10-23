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

/** The fields the user types into the add/edit form. */
export interface JobInput {
  title: string;
  company: string;
  location: string;
  url: string;
  description: string;
}
