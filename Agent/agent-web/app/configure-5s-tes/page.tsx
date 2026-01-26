import CredentialsTabs from "@/components/credentials/CredentialsTab";
import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Agent Web UI - Configure 5S-TES",
  description:
    "Configure 5S-TES TRE Admin with credentials to access Submission/Egress apps.",
};

export default function UpdateCredentials() {
  return (
    <>
      {/* Page Header */}
      <div className="mb-6">
        <h1 className="text-2xl font-bold">Configure 5S-TES</h1>
        <p className="mt-2 text-gray-600 dark:text-gray-400">
          Configure 5S-TES TRE Admin with credentials to access{" "}
          <a
            href="https://docs.federated-analytics.ac.uk/submission"
            target="_blank"
            rel="noopener noreferrer"
            className="font-semibold underline underline-offset-2"
          >
            Submission layer
          </a>{" "}
          and{" "}
          <a
            href="https://docs.federated-analytics.ac.uk/egress"
            target="_blank"
            rel="noopener noreferrer"
            className="font-semibold underline underline-offset-2"
          >
            Egress
          </a>
          .
        </p>
      </div>

      {/* Credentials Tabs */}
      <CredentialsTabs />
    </>
  );
}
