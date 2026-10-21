import { useEffect, useState } from "react";
import { api } from "../../api/client";
import type { AdminRoleDto, AdminUserDto } from "../../api/types";

export default function AdminUsers() {
  const [items, setItems] = useState<AdminUserDto[]>([]);
  const [roles, setRoles] = useState<AdminRoleDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [draftRoles, setDraftRoles] = useState<Record<string, string>>({});
  const [busyId, setBusyId] = useState<string | null>(null);

  const reload = async () => {
    const [usersRes, rolesRes] = await Promise.all([api.listUsers(1, 100), api.listAdminRoles()]);
    setItems(usersRes.items);
    setRoles(rolesRes);
    const drafts: Record<string, string> = {};
    for (const u of usersRes.items) {
      drafts[u.id] = u.roles[0] ?? rolesRes[0]?.name ?? "user";
    }
    setDraftRoles(drafts);
  };

  useEffect(() => {
    void reload().catch((err) => setError(err instanceof Error ? err.message : "Failed to load"));
  }, []);

  const toggleActive = async (user: AdminUserDto) => {
    setBusyId(user.id);
    setError(null);
    setMessage(null);
    try {
      await api.setUserActive(user.id, !user.isActive);
      setMessage(`${user.email} → ${!user.isActive ? "active" : "inactive"}`);
      await reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Update failed");
    } finally {
      setBusyId(null);
    }
  };

  const saveRole = async (user: AdminUserDto) => {
    const role = draftRoles[user.id];
    if (!role) return;
    setBusyId(user.id);
    setError(null);
    setMessage(null);
    try {
      await api.assignUserRoles(user.id, [role]);
      setMessage(`${user.email} roles → ${role}`);
      await reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Role update failed");
    } finally {
      setBusyId(null);
    }
  };

  if (error && items.length === 0) return <p className="portal-error">{error}</p>;

  return (
    <div className="portal-card">
      <h2>Users</h2>
      {error && <p className="portal-error">{error}</p>}
      {message && <p className="portal-success">{message}</p>}
      <table className="portal-table">
        <thead>
          <tr>
            <th>Email</th>
            <th>Name</th>
            <th>Roles</th>
            <th>Active</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {items.map((u) => (
            <tr key={u.id}>
              <td>{u.email}</td>
              <td>{u.displayName}</td>
              <td>
                <select
                  value={draftRoles[u.id] ?? u.roles[0] ?? ""}
                  onChange={(e) => setDraftRoles((prev) => ({ ...prev, [u.id]: e.target.value }))}
                >
                  {roles.map((r) => (
                    <option key={r.id} value={r.name}>
                      {r.name}
                    </option>
                  ))}
                </select>
              </td>
              <td>{u.isActive ? "Yes" : "No"}</td>
              <td>
                <div className="portal-table-actions">
                  <button
                    type="button"
                    className="btn btn--ghost"
                    disabled={busyId === u.id}
                    onClick={() => void saveRole(u)}
                  >
                    Save role
                  </button>
                  <button
                    type="button"
                    className="btn btn--ghost"
                    disabled={busyId === u.id}
                    onClick={() => void toggleActive(u)}
                  >
                    {u.isActive ? "Deactivate" : "Activate"}
                  </button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
