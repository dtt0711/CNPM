import { useEffect, useMemo, useState } from "react";
import { api } from "../../api/client";
import type { AnalyticsHourlyMetricDto } from "../../api/types";

export default function AdminAnalytics() {
  const [hourly, setHourly] = useState<AnalyticsHourlyMetricDto[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void api
      .analyticsSummary()
      .then((res) => setHourly(res.hourly))
      .catch((err) => setError(err instanceof Error ? err.message : "Failed to load"));
  }, []);

  const totals = useMemo(() => {
    const byKey = new Map<string, number>();
    for (const row of hourly) {
      byKey.set(row.metricKey, (byKey.get(row.metricKey) ?? 0) + row.count);
    }
    return [...byKey.entries()].sort((a, b) => b[1] - a[1]);
  }, [hourly]);

  if (error) return <p className="portal-error">{error}</p>;

  return (
    <div className="portal-card">
      <h2>Analytics summary</h2>
      <div className="portal-table-actions" style={{ marginBottom: "0.75rem" }}>
        <button
          type="button"
          className="btn btn--ghost"
          onClick={() => {
            void (async () => {
              try {
                const blob = await api.exportAnalyticsCsv();
                const url = URL.createObjectURL(blob);
                const a = document.createElement("a");
                a.href = url;
                a.download = "analytics-export.csv";
                a.click();
                URL.revokeObjectURL(url);
              } catch (err) {
                setError(err instanceof Error ? err.message : "Export failed");
              }
            })();
          }}
        >
          Export CSV
        </button>
      </div>
      <div className="portal-metrics">
        {totals.length === 0 ? (
          <p className="portal-muted">No metrics yet.</p>
        ) : (
          totals.map(([key, count]) => (
            <div key={key} className="portal-metric-card">
              <span className="portal-metric-key">{key}</span>
              <span className="portal-metric-value">{count}</span>
            </div>
          ))
        )}
      </div>
      {hourly.length > 0 && (
        <>
          <h3>Hourly breakdown</h3>
          <table className="portal-table">
            <thead>
              <tr>
                <th>Hour (UTC)</th>
                <th>Metric</th>
                <th>Count</th>
              </tr>
            </thead>
            <tbody>
              {hourly.slice(0, 50).map((row, i) => (
                <tr key={`${row.hourUtc}-${row.metricKey}-${i}`}>
                  <td>{new Date(row.hourUtc).toISOString()}</td>
                  <td>{row.metricKey}</td>
                  <td>{row.count}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </div>
  );
}
