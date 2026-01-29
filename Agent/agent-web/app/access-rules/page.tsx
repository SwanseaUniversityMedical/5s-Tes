import AccessRulesTable from "@/components/access-rules/AccessRulesTable";
import { authcheck } from "@/lib/auth-helpers";
import type { Metadata } from "next";
import { Button } from "@/components/ui/button";
import { PageHeader } from "@/components/core/page-header";


// Metadata for the Access Rules page
export const metadata: Metadata = {
  title: "Agent Web UI - Access Rules",
  description: "Manage access rules for the TRE Admin",
};

// Access Rules Page Component
export default async function AccessRules() {
  await authcheck("dare-tre-admin");

  return (
    <div>
      <PageHeader
        title="Access Rules"
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
      />
      <AccessRulesTable />
    </div>
  );
}