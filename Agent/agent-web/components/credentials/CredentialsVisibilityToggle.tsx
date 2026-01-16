"use client";

import { Eye, EyeOff } from "lucide-react";

// Props for CredentialsVisibilityToggle component

type CredentialsVisibilityToggleProps = {
  isVisible: boolean;
  onToggle: () => void;
};

// Creates Password Visibility Toggle Button

export default function CredentialsVisibilityToggle({
  isVisible,
  onToggle,
}: CredentialsVisibilityToggleProps) {
  return (
    <button
      type="button"
      onClick={onToggle}
      className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-500 hover:text-gray-700"
      aria-label={isVisible ? "Hide password" : "Show password"}
    >
      {isVisible ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
    </button>
  );
}
