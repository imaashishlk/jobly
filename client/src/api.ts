import type { ApplicationStatus, Cv, Job, JobInput, JobMatch } from "./types";

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

/** The text to show the user for any error: the API's own message, or a general one for unexpected errors. */
export function errorMessage(error: unknown): string {
  return error instanceof ApiError ? error.message : "Something went wrong. Please try again.";
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
  // A file upload sends FormData. The browser must set that Content-Type itself (it contains a random boundary),
  // so only JSON requests get the JSON header.
  const isFormData = init?.body instanceof FormData;

  let response: Response;
  try {
    response = await fetch(path, {
      ...init,
      headers: isFormData ? init?.headers : { "Content-Type": "application/json", ...init?.headers },
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

  // The web server answers 413 itself, with a title but no helpful detail.
  if (response.status === 413) return new ApiError("The file is too large. The limit is 5 MB.", 413, problem?.errorCode, problem?.traceId);

  // For a 400 from the [Required] / [MaxLength] checks, show the first field message ("The Title field is required.").
  const firstValidationMessage = problem?.errors ? Object.values(problem.errors).flat()[0] : undefined;
  const message = firstValidationMessage ?? problem?.detail ?? problem?.title ?? `The request failed (${response.status}).`;

  return new ApiError(message, response.status, problem?.errorCode, problem?.traceId);
}

// "Not there yet" (404) is a normal answer for the CV and for a job's match result. It becomes null instead of an error.
async function orNullWhenMissing<T>(call: () => Promise<T>): Promise<T | null> {
  try {
    return await call();
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null;
    throw error;
  }
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

  getCv: () => orNullWhenMissing(() => request<Cv>("/api/cv")),

  // The form field must be named "file": the API reads it by that name.
  uploadCv: (file: File) => {
    const form = new FormData();
    form.append("file", file);
    return request<Cv>("/api/cv/file", { method: "POST", body: form });
  },

  saveCvText: (text: string) => request<Cv>("/api/cv/text", { method: "PUT", body: JSON.stringify({ text }) }),

  deleteCv: () => request<void>("/api/cv", { method: "DELETE" }),

  getMatch: (jobId: string) => orNullWhenMissing(() => request<JobMatch>(`/api/jobs/${jobId}/match`)),

  // Can take minutes: the language model runs on the CPU.
  analyzeJob: (jobId: string) => request<JobMatch>(`/api/jobs/${jobId}/match`, { method: "POST" }),
};
