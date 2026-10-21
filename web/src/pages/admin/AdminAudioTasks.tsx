import { useEffect, useState } from "react";
import { api } from "../../api/client";
import type { AudioTaskDto } from "../../api/types";

function asTaskList(value: unknown): AudioTaskDto[] {
  return Array.isArray(value) ? (value as AudioTaskDto[]) : [];
}

function progressOf(task: AudioTaskDto): number {
  if (typeof task.progressPercent === "number") return task.progressPercent;
  const s = (task.status ?? "").toLowerCase();
  if (s === "done" || s === "failed" || s === "cancelled") return 100;
  if (s === "running") return 55;
  if (s === "paused") return 40;
  return 5;
}

function isActionable(task: AudioTaskDto): boolean {
  const id = task.id ?? "";
  return Boolean(id) && id !== "00000000-0000-0000-0000-000000000000";
}

export default function AdminAudioTasks() {
  const [tasks, setTasks] = useState<AudioTaskDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const controller = new AbortController();
    let cancelled = false;

    const refresh = async () => {
      try {
        const next = await api.listAudioTasks(80, controller.signal);
        if (cancelled) return;
        setTasks(asTaskList(next));
        setError(null);
      } catch (err) {
        if (cancelled || controller.signal.aborted) return;
        if (err instanceof DOMException && err.name === "AbortError") return;
        if (err instanceof Error && err.name === "AbortError") return;
        setError(err instanceof Error ? err.message : "Failed to load");
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    void refresh();
    const poll = window.setInterval(() => void refresh(), 2500);

    return () => {
      cancelled = true;
      controller.abort();
      window.clearInterval(poll);
    };
  }, []);

  const act = async (id: string, action: "pause" | "resume" | "cancel") => {
    if (!id || id === "00000000-0000-0000-0000-000000000000") return;
    setBusyId(id);
    setError(null);
    try {
      const updated =
        action === "pause"
          ? await api.pauseAudioTask(id)
          : action === "resume"
            ? await api.resumeAudioTask(id)
            : await api.cancelAudioTask(id);
      setTasks((prev) =>
        asTaskList(prev).map((t) => (t.id === id ? { ...t, ...updated } : t)),
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : "Action failed");
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="portal-card">
      <h2>Audio tasks</h2>
      <p className="portal-muted">Background TTS queue (max 3 parallel). Refreshing every 2.5s.</p>
      {error && <p className="portal-error">{error}</p>}
      {loading && tasks.length === 0 ? (
        <p className="portal-muted">Loading tasks…</p>
      ) : tasks.length === 0 ? (
        <p className="portal-muted">
          No recent tasks. Use <strong>Content → Regenerate all audio</strong> or play a place on the
          map.
        </p>
      ) : (
        <table className="portal-table">
          <thead>
            <tr>
              <th>Place</th>
              <th>Lang</th>
              <th>Status</th>
              <th>Progress</th>
              <th>Attempts</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {tasks.map((task, index) => {
              const status = (task.status ?? "").toLowerCase();
              const placeLabel =
                task.placeName ||
                (typeof task.placeId === "string" ? task.placeId.slice(0, 8) : "—");
              const canPause = isActionable(task) && (status === "pending" || status === "running");
              const canResume = isActionable(task) && (status === "paused" || status === "failed");
              const canCancel =
                isActionable(task) && status !== "done" && status !== "cancelled" && status !== "";
              const pct = progressOf(task);
              const rowKey = `${task.id ?? "x"}-${task.lang ?? "lang"}-${task.updatedAt ?? index}`;
              return (
                <tr key={rowKey}>
                  <td>
                    <div>{placeLabel}</div>
                    {task.error ? <div className="portal-muted">{task.error}</div> : null}
                  </td>
                  <td>{task.lang || "—"}</td>
                  <td>{task.status || "—"}</td>
                  <td>
                    <div className="portal-progress" aria-label={`${pct}%`}>
                      <div className="portal-progress__bar" style={{ width: `${pct}%` }} />
                    </div>
                    <span className="portal-muted">{pct}%</span>
                  </td>
                  <td>{task.attempts ?? 0}</td>
                  <td>
                    <div className="portal-table-actions">
                      {canPause && (
                        <button
                          type="button"
                          className="btn btn--ghost"
                          disabled={busyId === task.id}
                          onClick={() => void act(task.id, "pause")}
                        >
                          Pause
                        </button>
                      )}
                      {canResume && (
                        <button
                          type="button"
                          className="btn btn--ghost"
                          disabled={busyId === task.id}
                          onClick={() => void act(task.id, "resume")}
                        >
                          Resume
                        </button>
                      )}
                      {canCancel && (
                        <button
                          type="button"
                          className="btn btn--ghost"
                          disabled={busyId === task.id}
                          onClick={() => void act(task.id, "cancel")}
                        >
                          Cancel
                        </button>
                      )}
                      {!canPause && !canResume && !canCancel && (
                        <span className="portal-muted">—</span>
                      )}
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </div>
  );
}
