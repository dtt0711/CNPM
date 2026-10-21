import { useEffect, useState } from "react";
import { watchHybridConnectivity } from "../maps/hybridConnectivity";

/** `null` = still probing. */
export function useInternetStatus(): boolean | null {
  const [online, setOnline] = useState<boolean | null>(null);

  useEffect(() => {
    return watchHybridConnectivity({
      onInternetLost: () => setOnline(false),
      onInternetRestored: () => setOnline(true),
    });
  }, []);

  return online;
}
