"use client";

import { useState } from "react";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { CredentialType } from "@/types/update-credentials";

import CredentialsForm from "./CredentialsForm";
import CredentialsStatusBadge from "./CredentialsStatusBadge";

// Creates Credentials Tabs with Submission and Egress sections

export default function CredentialsTabs() {
  const [activeTab, setActiveTab] = useState<CredentialType>("submission");
  const [refreshKey, setRefreshKey] = useState(0);

  // Called when credentials are successfully updated
  const handleCredentialsUpdated = () => {
    setRefreshKey((prev) => prev + 1);
  };

  return (
    <Tabs
      defaultValue="submission"
      onValueChange={(value) => setActiveTab(value as CredentialType)}
      className="w-90"
    >
      {/* Tabs List with Status Badge */}
      <div className="flex items-center gap-4 border-b border-black pb-2 -mx-4 px-4">
        <TabsList>
          <TabsTrigger value="submission">Submission</TabsTrigger>
          <TabsTrigger value="egress">Egress</TabsTrigger>
        </TabsList>
        <CredentialsStatusBadge type={activeTab} refreshKey={refreshKey} />
      </div>

      {/* Tab Contents */}
      <TabsContent value="submission">
        <CredentialsForm type="submission" onSuccess={handleCredentialsUpdated} />
      </TabsContent>
      <TabsContent value="egress">
        <CredentialsForm type="egress" onSuccess={handleCredentialsUpdated} />
      </TabsContent>
    </Tabs>
  );
}