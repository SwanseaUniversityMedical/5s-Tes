"use client";

import { createContext, useContext } from "react";
import { useRefreshKey } from "@/lib/hooks/use-refresh-key";

/*
ValidationContext provides a shared mechanism for
triggering revalidation of UI components
(such as validation badges) after
access rule changes.

Why is this needed?
- When a rule is added, edited, or deleted, we want to re-run validation
and update any UI elements that depend on the validation state.

 */

/* ----- Types & Constants ----- */

type ValidationContextType = {
  refreshKey: number;
  triggerValidation: () => void;
};

const ValidationContext = createContext<ValidationContextType | undefined>(
  undefined
);

/* ----- Validation Context Provider & Hook ----- */

/* Provider component for ValidationContext. Wrap the component tree
with this to enable validation refresh signaling. */
export function ValidationProvider({
  children,
}: {
  children: React.ReactNode;
}) {
  const { refreshKey, bump } = useRefreshKey();

  return (
    <ValidationContext.Provider value={{ refreshKey, triggerValidation: bump }}>
      {children}
    </ValidationContext.Provider>
  );
}

/* ----- Custom Hook to use Validation Context ----- */

/* Custom hook to access the ValidationContext. Throws an error
 if used outside a ValidationProvider. */
export function useValidation() {
  const context = useContext(ValidationContext);
  if (!context) {
    throw new Error("useValidation must be used within a ValidationProvider");
  }
  return context;
}
