export interface Toast {
  id: number;
  kind: "success" | "error";
  text: string;
}

// aria-live makes screen readers announce new messages without moving the focus.
export default function Toasts({ toasts }: { toasts: Toast[] }) {
  return (
    <div className="toasts" aria-live="polite">
      {toasts.map((toast) => (
        <div key={toast.id} className={`toast toast-${toast.kind}`} role={toast.kind === "error" ? "alert" : "status"}>
          {toast.text}
        </div>
      ))}
    </div>
  );
}
