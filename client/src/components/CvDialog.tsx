import { useEffect, useRef, useState, type DragEvent } from "react";
import { api, errorMessage } from "../api";
import type { Cv } from "../types";

// The same limits as the API (CvService.MaxFileBytes and Cv.MaxTextLength). The API checks them again.
const MAX_FILE_BYTES = 5 * 1024 * 1024;
const MAX_TEXT_CHARACTERS = 30_000;

interface CvDialogProps {
  onClose: () => void;
  /** Called after every change, so the rest of the app knows whether a CV exists. */
  onChanged: (cv: Cv | null) => void;
  onNotify: (kind: "success" | "error", text: string) => void;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString(undefined, { day: "numeric", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" });
}

export default function CvDialog({ onClose, onChanged, onNotify }: CvDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [cv, setCv] = useState<Cv | null>(null);
  const [text, setText] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [draggingOver, setDraggingOver] = useState(false);

  // showModal() gives us the dark backdrop, focus handling and the Escape key for free.
  useEffect(() => {
    dialogRef.current?.showModal();
  }, []);

  useEffect(() => {
    let cancelled = false;
    api
      .getCv()
      .then((current) => {
        if (cancelled) return;
        setCv(current);
        setText(current?.text ?? "");
      })
      .catch((loadError) => {
        if (!cancelled) setError(errorMessage(loadError));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  // Runs one action (upload, save, delete), shows its result, and keeps the dialog and the app in step.
  async function run(action: () => Promise<Cv | null>, successMessage: string) {
    setBusy(true);
    setError(null);
    try {
      const next = await action();
      setCv(next);
      setText(next?.text ?? "");
      setConfirmingDelete(false);
      onChanged(next);
      onNotify("success", successMessage);
    } catch (actionError) {
      setError(errorMessage(actionError));
    } finally {
      setBusy(false);
    }
  }

  function chooseFile(file: File | undefined) {
    if (!file) return;

    // Quick checks for a friendly message at once. The API checks again, and also checks what is inside the file.
    const extension = file.name.toLowerCase().split(".").pop();
    if (extension !== "pdf" && extension !== "docx") {
      setError("Only PDF and DOCX files are supported.");
      return;
    }
    if (file.size > MAX_FILE_BYTES) {
      setError("The file is too large. The limit is 5 MB.");
      return;
    }

    void run(() => api.uploadCv(file), "CV uploaded.");
  }

  function handleDrop(event: DragEvent) {
    event.preventDefault();
    setDraggingOver(false);
    chooseFile(event.dataTransfer.files[0]);
  }

  const textChanged = text.trim() !== (cv?.text ?? "");

  return (
    <dialog ref={dialogRef} className="dialog dialog-wide" onClose={onClose} aria-labelledby="cv-dialog-title">
      <div className="dialog-stack">
        <h2 id="cv-dialog-title">Your CV</h2>
        <p className="dialog-hint">
          Upload a PDF or DOCX, or paste the text. It is stored in your own database and read by the language model on your own computer.
        </p>

        {loading ? (
          <p role="status">Loading…</p>
        ) : (
          <>
            {cv && (
              <p className="cv-meta">
                {cv.source === "Uploaded" ? `From ${cv.fileName ?? "a file"}` : "Pasted text"} · {cv.characterCount.toLocaleString()} characters · updated{" "}
                {formatDate(cv.updatedAt)}
              </p>
            )}

            <div
              className="dropzone"
              data-over={draggingOver}
              onDragOver={(event) => {
                event.preventDefault();
                setDraggingOver(true);
              }}
              onDragLeave={() => setDraggingOver(false)}
              onDrop={handleDrop}
            >
              <span>Drop a PDF or DOCX here, or</span>
              <button type="button" className="button" disabled={busy} onClick={() => fileInputRef.current?.click()}>
                {cv ? "Replace with a file" : "Choose a file"}
              </button>
              <input
                ref={fileInputRef}
                type="file"
                accept=".pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                hidden
                onChange={(event) => {
                  chooseFile(event.target.files?.[0]);
                  event.target.value = ""; // Lets the same file be chosen again later.
                }}
              />
            </div>

            <label>
              CV text {cv ? "(you can fix mistakes from the file here)" : "(or paste it here)"}
              <textarea
                id="cv-text"
                value={text}
                onChange={(event) => setText(event.target.value)}
                rows={14}
                maxLength={MAX_TEXT_CHARACTERS + 5_000}
                spellCheck={false}
              />
            </label>
            <p className="cv-count" data-over-limit={text.trim().length > MAX_TEXT_CHARACTERS}>
              {text.trim().length.toLocaleString()} / {MAX_TEXT_CHARACTERS.toLocaleString()} characters
            </p>
          </>
        )}

        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}

        <div className="dialog-actions dialog-actions-spread">
          {cv && !confirmingDelete && (
            <button type="button" className="button button-quiet-danger" disabled={busy} onClick={() => setConfirmingDelete(true)}>
              Delete CV
            </button>
          )}
          {cv && confirmingDelete && (
            <span className="confirm">
              Delete your CV?
              <button
                type="button"
                className="button button-danger"
                disabled={busy}
                onClick={() => void run(async () => { await api.deleteCv(); return null; }, "CV deleted.")}
              >
                Yes, delete
              </button>
              <button type="button" className="button" onClick={() => setConfirmingDelete(false)}>
                No
              </button>
            </span>
          )}
          <span className="dialog-actions-right">
            <button type="button" className="button" onClick={() => dialogRef.current?.close()}>
              Close
            </button>
            <button
              type="button"
              className="button button-primary"
              disabled={busy || loading || !textChanged || text.trim().length === 0}
              onClick={() => void run(() => api.saveCvText(text), "CV text saved.")}
            >
              {busy ? "Working…" : "Save text"}
            </button>
          </span>
        </div>
      </div>
    </dialog>
  );
}
