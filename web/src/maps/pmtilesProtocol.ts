import maplibregl from "maplibre-gl";
import { Protocol } from "pmtiles";

let registered = false;
let protocol: Protocol | null = null;

/**
 * Registers the pmtiles:// protocol with MapLibre (idempotent).
 * `metadata: true` is required so TileJSON includes OpenMapTiles `vector_layers`;
 * without it MapLibre paints only the background color.
 */
export function ensurePmtilesProtocol(): void {
  if (registered) return;
  protocol = new Protocol({ metadata: true });
  maplibregl.addProtocol("pmtiles", protocol.tile);
  registered = true;
}

export function getPmtilesProtocol(): Protocol | null {
  return protocol;
}
