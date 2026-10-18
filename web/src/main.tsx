import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { registerSW } from "virtual:pwa-register";

import "./i18n";
import "./styles/global.css";
import App from "./App";

// Registers the Workbox-generated service worker (app shell + audio/map
// runtime caches, see vite.config.ts). Auto-updates silently in the background.
registerSW({ immediate: true });

const rootEl = document.getElementById("root");
if (!rootEl) {
  throw new Error("Root element '#root' not found");
}

createRoot(rootEl).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
