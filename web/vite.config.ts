/// <reference types="vitest/config" />
import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import { VitePWA } from "vite-plugin-pwa";

// TourismHcm audio guide PWA.
// - Dev server proxies /api to the backend (default: http://localhost:8080).
// - vite-plugin-pwa (Workbox) precaches the app shell and runtime-caches
//   map tiles and narration audio (grouped per language) for offline use.
export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: "autoUpdate",
      includeAssets: ["icons/*.svg"],
      manifest: {
        id: "/",
        name: "TourismHcm Audio Guide",
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
        // App shell / build assets precached automatically by Workbox.
        globPatterns: ["**/*.{js,css,html,svg,png,ico,webmanifest}"],
        navigateFallbackDenylist: [/^\/api\//],
        runtimeCaching: [
          {
            // Narration audio, grouped per-language by URL (?lang=xx or /audio/xx/...).
            urlPattern: ({ request, url }) =>
              request.destination === "audio" ||
              /\.(mp3|wav|ogg|m4a|aac)(\?.*)?$/i.test(url.pathname + url.search),
            handler: "CacheFirst",
            options: {
              cacheName: "tourismhcm-audio-cache",
              expiration: {
                maxEntries: 300,
                maxAgeSeconds: 60 * 60 * 24 * 60, // 60 days
              },
              cacheableResponse: { statuses: [0, 200] },
              matchOptions: { ignoreVary: true },
            },
          },
          {
            // Map tiles / style assets.
            urlPattern: ({ url, request }) =>
              request.destination === "image" ||
              /tile|maptiler|demotiles|openstreetmap/i.test(url.hostname),
            handler: "CacheFirst",
            options: {
              cacheName: "tourismhcm-map-tiles",
              expiration: {
                maxEntries: 500,
                maxAgeSeconds: 60 * 60 * 24 * 30, // 30 days
              },
              cacheableResponse: { statuses: [0, 200] },
            },
          },
          {
            // Read-only API calls (places list/detail, offline pack manifest).
            urlPattern: ({ url, sameOrigin }) =>
              sameOrigin && url.pathname.startsWith("/api/"),
            handler: "NetworkFirst",
            method: "GET",
            options: {
              cacheName: "tourismhcm-api-cache",
              networkTimeoutSeconds: 4,
              expiration: {
                maxEntries: 200,
                maxAgeSeconds: 60 * 60 * 24, // 1 day
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
    proxy: {
      // Prefer local `dotnet run` (5020). For Docker API: set env VITE_API_PROXY_TARGET=http://localhost:8080
      "/api": {
        target: "http://localhost:5020",
        changeOrigin: true,
      },
      "/audio": {
        target: "http://localhost:5020",
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
});
