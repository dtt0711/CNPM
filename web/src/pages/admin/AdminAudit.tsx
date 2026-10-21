import { useEffect, useState } from "react";
import { api } from "../../api/client";
import type { AuditLogDto } from "../../api/types";

export default function AdminAudit() {
  const [items, setItems] = useState<AuditLogDto[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void api
      .auditLogs({ pageSize: 100 })
      .then((res) => setItems(res.items))
      .catch((err) => setError(err instanceof Error ? err.message : "Failed to load"));
  }, []);

  if (error) return <p className="portal-error">{error}</p>;

  return (
    <div className="portal-card">
      <h2>Audit log</h2>
      <table className="portal-table">
        <thead>
          <tr>
            <th>When</th>
            <th>Action</th>
            <th>Entity</th>
            <th>Details</th>
          </tr>
        </thead>
        <tbody>
          {items.map((row) => (
            <tr key={row.id}>
              <td>{new Date(row.createdAt).toLocaleString()}</td>
              <td>{row.action}</td>
              <td>
                {row.entityType} / {row.entityId}
              </td>
              <td>{row.details ?? "—"}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
