"use client";

import { useEffect, useRef, useState } from "react";
import { AlertCircle, CheckCircle, Loader2 } from "lucide-react";


/* ----- Type Definitions ----- */

// Result type returned by the check function
export type StatusBadgeResult<T> = {
  success: boolean;
  data?: T;
  error?: unknown;
};

// Props for StatusBadge component
export type StatusBadgeProps = {
  refreshKey?: number;
  check: () => Promise<StatusBadgeResult<boolean>>;

  loadingText?: string;
  validText?: string;
  invalidText?: string;
  unknownText?: string;

  delayMs?: number;
  className?: string;
};

// Internal state type for loading and validation status
type StatusState = {
  loading: boolean;
  value: boolean | null;
};

// Component to display the status badge
export default function StatusBadge({
  refreshKey,
  check,
  loadingText = "Checking...",
  validText = "Valid",
  invalidText = "Invalid",
  unknownText = "Unknown",
  delayMs = 0,
  className,
}: StatusBadgeProps) {
  const [state, setState] = useState<StatusState>({ loading: true, value: null });
  const requestId = useRef(0);

  useEffect(() => {
    let active = true;
    const id = ++requestId.current;

    const run = async () => {
      setState({ loading: true, value: null });

      const [result] = await Promise.all([
        check(),
        delayMs > 0 ? new Promise((r) => setTimeout(r, delayMs)) : Promise.resolve(),
      ]);

      if (!active) return;
      if (id !== requestId.current) return;

      setState({
        loading: false,
        value: result.success ? (result.data ?? null) : null,
      });
    };

    run();

    return () => {
      active = false;
    };
  }, [check, refreshKey, delayMs]);

  // Render Valid Component based on status
  if (state.loading) {
    return (
      <span className={`inline-flex items-center text-sm text-gray-500 dark:text-gray-400 ${className ?? ""}`}>
        <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
        {loadingText}
      </span>
    );
  }

  if (state.value === true) {
    return (
      <span className={`inline-flex items-center text-sm text-green-600 dark:text-green-600 ${className ?? ""}`}>
        <CheckCircle className="mr-1.5 h-4 w-4" />
        {validText}
      </span>
    );
  }

  if (state.value === false) {
    return (
      <span className={`inline-flex items-center text-sm text-amber-600 dark:text-amber-600 ${className ?? ""}`}>
        <AlertCircle className="mr-1.5 h-4 w-4" />
        {invalidText}
      </span>
    );
  }

  return (
    <span className={`inline-flex items-center text-sm text-gray-500 dark:text-gray-400 ${className ?? ""}`}>
      <AlertCircle className="mr-1.5 h-4 w-4" />
      {unknownText}
    </span>
  );
}
