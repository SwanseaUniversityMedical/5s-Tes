"use client";

import { useState } from "react";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import CredentialsForm from "./CredentialsForm";
import CredentialsHelpTooltip from "./CredentialsHelpTooltip";
import { CredentialType } from "@/types/update-credentials";

// Define default styling for Tabs Components
const TabsDefaultStyling =
  "data-[state=active]:bg-gray-900 data-[state=active]:text-white";

// Creates Credentials Tabs with Submission and Egress sections
export default function CredentialsTabs() {
  const [activeTab, setActiveTab] = useState<CredentialType>("submission");

  return (
    <Tabs
      defaultValue="submission"
      className="w-88"
      onValueChange={(value) => setActiveTab(value as CredentialType)}
    >
      {/* Tabs List + Help Tooltip */}
      <div className="flex items-center gap-2 border-b-2 border-black pb-2 -mx-4 px-4">
        <TabsList className="bg-transparent border border-gray-300">
          <TabsTrigger value="submission" className={TabsDefaultStyling}>
            Submission
          </TabsTrigger>
          <TabsTrigger value="egress" className={TabsDefaultStyling}>
            Egress
          </TabsTrigger>
        </TabsList>

        {/* Help Tooltip */}
        <CredentialsHelpTooltip type={activeTab} />
      </div>

      {/* Tab Contents */}
      <TabsContent value="submission">
        <CredentialsForm type="submission" />
      </TabsContent>
      <TabsContent value="egress">
        <CredentialsForm type="egress" />
      </TabsContent>
    </Tabs>
  );
}
