/// <reference types="vitest/config" />
import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";
import { VitePWA } from "vite-plugin-pwa";

// TourismHcm audio guide PWA.
// - Dev server proxies /api to the backend (default: Docker API http://localhost:8080).
// - vite-plugin-pwa (Workbox) precaches the app shell and runtime-caches
//   map tiles and narration audio (grouped per language) for offline use.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, ".", "");
  // Default matches local `dotnet run` (:5020). Docker Compose: set VITE_API_PROXY_TARGET=http://localhost:8080 in web/.env
  const apiProxyTarget = (env.VITE_API_PROXY_TARGET || "http://localhost:5020").replace(/\/+$/, "");

  return {
    plugins: [
      react(),
      VitePWA({
        registerType: "autoUpdate",
        includeAssets: ["icons/*.svg"],
        manifest: {
          id: "/",
          name: "Du lịch TP.HCM",
          short_name: "TourismHcm",
          description:
            "Audio guide app for Ho Chi Minh City attractions - offline-friendly multilingual narration.",
          theme_color: "#0b3d4c",
          background_color: "#07262f",
          display: "standalone",
          start_url: "/",
          scope: "/",
          lang: "vi",
          icons: [
            {
              src: "icons/icon-192.svg",
              sizes: "192x192",
              type: "image/svg+xml",
              purpose: "any",
            },
            {
              src: "icons/icon-512.svg",
              sizes: "512x512",
              type: "image/svg+xml",
              purpose: "any",
            },
            {
              src: "icons/icon-maskable.svg",
              sizes: "512x512",
              type: "image/svg+xml",
              purpose: "maskable",
            },
          ],
        },
        workbox: {
          globPatterns: ["**/*.{js,css,html,svg,png,ico,webmanifest}"],
          navigateFallbackDenylist: [/^\/api\//],
          runtimeCaching: [
            {
              urlPattern: ({ request, url }) =>
                request.destination === "audio" ||
                /\.(mp3|wav|ogg|m4a|aac)(\?.*)?$/i.test(url.pathname + url.search),
              handler: "CacheFirst",
              options: {
                cacheName: "tourismhcm-audio-cache",
                expiration: {
                  maxEntries: 300,
                  maxAgeSeconds: 60 * 60 * 24 * 60,
                },
                cacheableResponse: { statuses: [0, 200] },
                matchOptions: { ignoreVary: true },
              },
            },
            {
              urlPattern: ({ url, request }) =>
                request.destination === "image" ||
                /tile|maptiler|demotiles|openstreetmap/i.test(url.hostname),
              handler: "CacheFirst",
              options: {
                cacheName: "tourismhcm-map-tiles",
                expiration: {
                  maxEntries: 500,
                  maxAgeSeconds: 60 * 60 * 24 * 30,
                },
                cacheableResponse: { statuses: [0, 200] },
              },
            },
            {
              urlPattern: ({ url, sameOrigin }) =>
                sameOrigin && url.pathname.startsWith("/api/"),
              handler: "NetworkFirst",
              method: "GET",
              options: {
                cacheName: "tourismhcm-api-cache",
                networkTimeoutSeconds: 4,
                expiration: {
                  maxEntries: 200,
                  maxAgeSeconds: 60 * 60 * 24,
                },
                cacheableResponse: { statuses: [0, 200] },
              },
            },
          ],
        },
        devOptions: {
          enabled: false,
        },
      }),
    ],
    server: {
      // Cloudflare / ngrok quick tunnels send a public Host header.
      allowedHosts: true,
      proxy: {
        "/api": {
          target: apiProxyTarget,
          changeOrigin: true,
        },
        "/audio": {
          target: apiProxyTarget,
          changeOrigin: true,
        },
        "/static": {
          target: apiProxyTarget,
          changeOrigin: true,
        },
      },
    },
    test: {
      environment: "jsdom",
      globals: true,
      include: ["src/**/*.{test,spec}.{ts,tsx}"],
      css: false,
    },
  };
});
