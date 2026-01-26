"use client";

import { useState } from "react";
import { useRefreshKey } from "@/lib/hooks/use-refresh-key";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { CredentialType } from "@/types/update-credentials";

import CredentialsForm from "./CredentialsForm";
import CredentialsStatusBadge from "./CredentialsStatusBadge";
import { FieldSeparator } from "../ui/field";

// Creates Credentials Tabs with Submission and Egress sections

export default function CredentialsTabs() {
  const [activeTab, setActiveTab] = useState<CredentialType>("submission");
  const { refreshKey, bump } = useRefreshKey();

  return (
    <Tabs
      defaultValue="submission"
      onValueChange={(value) => setActiveTab(value as CredentialType)}
      className="w-90"
    >
      {/* Tabs List with Status Badge */}
      <div className="flex items-center gap-4 -mx-4 px-2">
        <TabsList>
          <TabsTrigger value="submission">Submission</TabsTrigger>
          <TabsTrigger value="egress">Egress</TabsTrigger>
        </TabsList>
        <CredentialsStatusBadge type={activeTab} refreshKey={refreshKey} />
      </div>
      <FieldSeparator />

      {/* Tab Contents */}
      <TabsContent value="submission">
        <CredentialsForm type="submission" onSuccess={bump} />
      </TabsContent>
      <TabsContent value="egress">
        <CredentialsForm type="egress" onSuccess={bump} />
      </TabsContent>
    </Tabs>
  );
}
