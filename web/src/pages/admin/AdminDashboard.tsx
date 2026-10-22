import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { api } from "../../api/client";

const TILES = [
  {
    to: "/admin/places",
    title: "Places",
    desc: "Sửa địa chỉ, giờ mở cửa, giá vé, ảnh và tọa độ.",
    tag: "Nội dung",
  },
  {
    to: "/admin/audio-tasks",
    title: "Audio tasks",
    desc: "Theo dõi TTS, pause / resume / cancel theo thời gian thực.",
    tag: "Audio",
  },
  {
    to: "/admin/content",
    title: "Content",
    desc: "Bản dịch và regenerate audio theo ngôn ngữ.",
    tag: "Nội dung",
  },
  {
    to: "/admin/users",
    title: "Users",
    desc: "Quản lý người dùng, vai trò và trạng thái.",
    tag: "Hệ thống",
  },
  {
    to: "/admin/audit",
    title: "Audit",
    desc: "Nhật ký thao tác quản trị gần đây.",
    tag: "Hệ thống",
  },
  {
    to: "/admin/analytics",
    title: "Analytics",
    desc: "Tóm tắt số liệu và xuất báo cáo.",
    tag: "Số liệu",
  },
] as const;

export default function AdminDashboard() {
  const [activeDevices, setActiveDevices] = useState<number | null>(null);

  useEffect(() => {
    let cancelled = false;
    const load = () => {
      void api
        .analyticsSummary()
        .then((res) => {
          if (!cancelled) setActiveDevices(res.activeDevices);
        })
        .catch(() => {
          if (!cancelled) setActiveDevices(null);
        });
    };
    load();
    const timer = window.setInterval(load, 15_000);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, []);

  return (
    <div className="admin-dash">
      <section className="admin-dash__hero portal-card">
        <div>
          <p className="admin-dash__eyebrow">TourismHcm · TP. Hồ Chí Minh</p>
          <h2>Bảng điều khiển</h2>
          <p className="portal-muted">
            Chỉnh sửa địa điểm, theo dõi audio và xem analytics cho tour audio đa ngôn ngữ.
          </p>
        </div>
        <div className="admin-dash__hero-aside">
          <Link to="/admin/analytics" className="admin-dash__live" title="Cập nhật mỗi 15 giây">
            <span className="admin-live-dot" aria-hidden="true" />
            <span className="admin-dash__live-count">{activeDevices ?? "—"}</span>
            <span className="admin-dash__live-label">thiết bị đang truy cập</span>
          </Link>
          <Link to="/admin/places" className="btn btn--primary">
            Mở Places
          </Link>
        </div>
      </section>

      <div className="admin-dash__grid">
        {TILES.map((tile) => (
          <Link key={tile.to} to={tile.to} className="admin-dash__tile portal-card">
            <span className="admin-dash__tag">{tile.tag}</span>
            <h3>{tile.title}</h3>
            <p>{tile.desc}</p>
          </Link>
        ))}
      </div>
    </div>
  );
}
