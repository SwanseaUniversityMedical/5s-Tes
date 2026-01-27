import AccessRulesTable from "@/components/access-rules/AccessRulesTable";
import { authcheck } from "@/lib/auth-helpers";
import type { Metadata } from "next";


// Metadata for the Access Rules page
export const metadata: Metadata = {
  title: "Agent Web UI - Access Rules",
  description: "Manage access rules for the TRE Admin",
};

// Access Rules Page Component
export default async function AccessRules() {
  await authcheck("dare-tre-admin");

  return (
    <div className="pb-8">

      <h1 className="text-2xl pb-3 font-bold">Access Rules</h1>
      <AccessRulesTable />
    </div>
  );
}