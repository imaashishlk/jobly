import type { ApplicationStatus, Job, JobInput } from "./types";

/** An error answer from the API (ProblemDetails). The message is safe to show to the user. */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly errorCode?: string,
    readonly traceId?: string,
  ) {
    super(message);
  }
}

// ProblemDetails is the standard error JSON the API sends. Validation errors also carry an "errors" object.
interface ProblemDetails {
  title?: string;
  detail?: string;
  errorCode?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(path, {
      ...init,
      headers: { "Content-Type": "application/json", ...init?.headers },
    });
  } catch {
    // fetch only throws when the server can't be reached at all.
    throw new ApiError("Cannot reach the server. Is the API running?", 0);
  }

  if (!response.ok) throw await toApiError(response);

  // 204 No Content (delete) has no body to read.
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

async function toApiError(response: Response): Promise<ApiError> {
  const problem = (await response.json().catch(() => null)) as ProblemDetails | null;

  // For a 400 from the [Required] / [MaxLength] checks, show the first field message ("The Title field is required.").
  const firstValidationMessage = problem?.errors ? Object.values(problem.errors).flat()[0] : undefined;
  const message = firstValidationMessage ?? problem?.detail ?? problem?.title ?? `The request failed (${response.status}).`;

  return new ApiError(message, response.status, problem?.errorCode, problem?.traceId);
}

// The form keeps empty text boxes as "". The API expects null for "not filled in" (an empty URL would fail its URL check).
function toBody(input: JobInput) {
  return {
    title: input.title.trim(),
    company: input.company.trim(),
    location: input.location.trim() || null,
    url: input.url.trim() || null,
    description: input.description.trim(),
  };
}

export const api = {
  listJobs: () => request<Job[]>("/api/jobs"),

  createJob: (input: JobInput) => request<Job>("/api/jobs", { method: "POST", body: JSON.stringify(toBody(input)) }),

  updateJob: (id: string, input: JobInput) =>
    request<Job>(`/api/jobs/${id}`, { method: "PUT", body: JSON.stringify(toBody(input)) }),

  changeStatus: (id: string, status: ApplicationStatus) =>
    request<Job>(`/api/jobs/${id}/status`, { method: "PUT", body: JSON.stringify({ status }) }),

  updateNotes: (id: string, notes: string) =>
    request<Job>(`/api/jobs/${id}/notes`, { method: "PUT", body: JSON.stringify({ notes }) }),

  deleteJob: (id: string) => request<void>(`/api/jobs/${id}`, { method: "DELETE" }),
};
