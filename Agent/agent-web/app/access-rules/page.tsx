import AccessRulesTable from "@/components/access-rules/AccessRulesTable";
import { authcheck } from "@/lib/auth-helpers";
import type { Metadata } from "next";
import { Button } from "@/components/ui/button";
import { PageHeader } from "@/components/core/page-header";

import { getAccessRules } from "@/api/access-rules";
import { FetchError } from "@/components/core/fetch-error";
import { ValidationProvider } from "@/components/access-rules/ValidationContext";
import RulesValidationBadge from "@/components/access-rules/status-badge/RulesValidationBadge";

// Metadata for the Access Rules page
export const metadata: Metadata = {
  title: "Agent Web UI - Access Rules",
  description: "Manage access rules for the TRE Admin",
};

// Access Rules Page Component
export default async function AccessRules() {
  await authcheck("dare-tre-admin");

  const result = await getAccessRules();

  if (!result.success) {
    return <FetchError error={result.error} />;
  }

  // Destructure rules and info from single API call
  const { rules, info } = result.data;

  return (
    <ValidationProvider>
      <div>
        <PageHeader
          title="Access Rules"
          action={<RulesValidationBadge className="mt-1" />}
          description={
            <>
              Configure the{" "}
              <a
                href="https://docs.federated-analytics.ac.uk/submission/tasks/run_analysis#update-the-dmn-rules"
                target="_blank"
                rel="noopener noreferrer"
              >
                <Button
                  variant="link"
                  className="p-0 font-semibold text-md cursor-pointer"
                >
                  DMN Rules
                </Button>
              </a>{" "}
              to access TRE Database.
            </>
          }
          className="mb-0"
        />
        <AccessRulesTable data={rules} decisionInfo={info} />
      </div>
    </ValidationProvider>
  );
}
