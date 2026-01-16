"use client";

import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import CredentialsForm from "./CredentialsForm";

// Creates Credentials Tabs with Submission and Egress sections

export default function CredentialsTabs() {
  return (
    <Tabs
      defaultValue="submission"
      className="w-90"
    >
      {/* Tabs List */}
      <div className="flex items-center gap-2 border-b border-black pb-2 -mx-4 px-4">
        <TabsList>
          <TabsTrigger value="submission">
            Submission
          </TabsTrigger>
          <TabsTrigger value="egress">
            Egress
          </TabsTrigger>
        </TabsList>
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
