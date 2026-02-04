"use client";

import { useCallback } from "react";
import StatusBadge from "@/components/status-badge";
import { validateAccessRules } from "@/api/access-rules";
import { useValidation } from "../ValidationContext";

type RulesValidationBadgeProps = {
  className?: string;
};

export default function RulesValidationBadge({
  className,
}: RulesValidationBadgeProps) {
  const { refreshKey } = useValidation();

  const checkValidation = useCallback(async () => {
    const result = await validateAccessRules();

    return {
      success: result.success,
      data: result.success ? result.data.success : false,
    };
  }, []);

  return (
    <StatusBadge
      refreshKey={refreshKey}
      check={checkValidation}
      loadingText="Validating rules..."
      validText="DMN Rules are valid"
      invalidText="Rules validation failed"
      unknownText="Unable to validate"
      delayMs={1000}
      className={className}
    />
  );
}
