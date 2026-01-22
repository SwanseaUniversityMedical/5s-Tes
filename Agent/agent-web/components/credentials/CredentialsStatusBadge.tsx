"use client";

import { useState, useEffect } from "react";
import { CheckCircle, AlertCircle, Loader2 } from "lucide-react";
import { checkCredentialsValid } from "@/api/credentials";
import { CredentialType } from "@/types/update-credentials";

// Props for CredentialsStatusBadge component

type CredentialsStatusBadgeProps = {
  type: CredentialType;
  refreshKey?: number;
};

// Component to display the status badge for credentials

export default function CredentialsStatusBadge({ type, refreshKey }: CredentialsStatusBadgeProps) {
  const [status, setStatus] = useState<{
    loading: boolean;
    valid: boolean | null;
  }>({
    loading: true,
    valid: null,
  });

  useEffect(() => {
    const checkStatus = async () => {
      setStatus({ loading: true, valid: null });

      // Run API call and 2 second delay in parallel
      const [result] = await Promise.all([
        checkCredentialsValid(type),
        new Promise((resolve) => setTimeout(resolve, 1000)),
      ]);

      setStatus({
        loading: false,
        valid: result.success ? result.data : null,
      });
    };

    checkStatus();
  }, [type, refreshKey]);


  {/* Render Valid Component based on status */}

  if (status.loading) {
    return (
      <span className="inline-flex items-center text-sm text-gray-500">
        <Loader2 className="mr-1 h-4 w-4 animate-spin" />
        Checking...
      </span>
    );
  }

  if (status.valid === true) {
    return (
      <span className="inline-flex items-center text-sm text-green-600">
        <CheckCircle className="mr-1 h-4 w-4" />
        Saved Credentials Valid
      </span>
    );
  }

  if (status.valid === false) {
    return (
      <span className="inline-flex items-center text-sm text-amber-600">
        <AlertCircle className="mr-1 h-4 w-4" />
        Not configured
      </span>
    );
  }

  return null;
}