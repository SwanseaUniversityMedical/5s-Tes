import CredentialsTabs from "@/components/credentials/CredentialsTab";
import { Button } from "@/components/ui/button";
import { authcheck } from "@/lib/auth-helpers";
import type { Metadata } from "next";
import { PageHeader } from "@/components/core/page-header";

export const metadata: Metadata = {
  title: "Agent Web UI - Configure 5S-TES",
  description:
    "Configure 5S-TES TRE Admin with credentials to access Submission/Egress apps.",
};

export default async function UpdateCredentials() {
  await authcheck("dare-tre-admin");

  return (
    <>
      {/* Page Header */}
      <PageHeader
        title="Configure 5S-TES"
        description={
          <>
            Configure 5S-TES TRE Admin with credentials to access{" "}
          <a
            href="https://docs.federated-analytics.ac.uk/submission"
            target="_blank"
            rel="noopener noreferrer"
          >
            <Button
              variant="link"
              className="p-0 font-semibold text-md cursor-pointer"
            >
              Submission layer
            </Button>
          </a>{" "}
          and{" "}
          <a
            href="https://docs.federated-analytics.ac.uk/egress"
            target="_blank"
            rel="noopener noreferrer"
          >
            <Button
              variant="link"
              className="p-0 font-semibold text-md cursor-pointer"
            >
              Egress
            </Button>
          </a>
          .
        </>
        }
      />
      <CredentialsTabs />
    </>
  );
}
