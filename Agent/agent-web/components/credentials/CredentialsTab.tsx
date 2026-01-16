"use client";

import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import CredentialsForm from "./CredentialsForm";

// Define default styling for Tabs Components
const TabsDefaultStyling =
  "data-[state=active]:bg-gray-900 data-[state=active]:text-white";

// Creates Credentials Tabs with Submission and Egress sections
export default function CredentialsTabs() {
  return (
    <Tabs
      defaultValue="submission"
      className="w-90"
    >
      {/* Tabs List + Help Tooltip */}
      <div className="flex items-center gap-2 border-b border-black pb-2 -mx-4 px-4">
        <TabsList className="bg-transparent border border-gray-300">
          <TabsTrigger value="submission" className={TabsDefaultStyling}>
            Submission
          </TabsTrigger>
          <TabsTrigger value="egress" className={TabsDefaultStyling}>
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
