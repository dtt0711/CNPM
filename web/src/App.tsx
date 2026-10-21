import { Navigate, Route, Routes } from "react-router-dom";
import MapApp from "./pages/MapApp";
import LoginPage from "./pages/LoginPage";
import AdminPortal from "./pages/admin/AdminPortal";

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<MapApp />} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/admin/*" element={<AdminPortal />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
