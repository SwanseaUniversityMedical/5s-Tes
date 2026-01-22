"use client";

import { useCallback, useState } from "react";

// Custom hook to manage a refresh key for re-rendering components

export function useRefreshKey(initial = 0) {
  const [refreshKey, setRefreshKey] = useState(initial);

  const bump = useCallback(() => {
    setRefreshKey((prev) => prev + 1);
  }, []);

  return { refreshKey, bump };
}