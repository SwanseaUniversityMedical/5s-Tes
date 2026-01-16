"use client";

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { HelpCircle, ExternalLink } from "lucide-react";
import { CredentialType } from "@/types/update-credentials";

// Props for CredentialsHelpTooltip component

type CredentialsHelpTooltipProps = {
  type: CredentialType;
};

// Documentation Links for each Credential Type

const DOCS_LINK: Record<CredentialType, string> = {
  submission:
    "https://docs.federated-analytics.ac.uk/submission/guides/addtreagent#enter-submission-credentials",
  egress:
    "https://docs.federated-analytics.ac.uk/tre_agent/guides/connecttoegress#create-and-add-a-user-to-access-the-egress-from-the-tre-agent",
};

// Creates Help Tooltip for Credentials Tabs

export default function CredentialsHelpTooltip({
  type,
}: CredentialsHelpTooltipProps) {
  return (
    <TooltipProvider>
      <Tooltip>
        {/* Tooltip Trigger (Help Icon) */}
        <TooltipTrigger asChild>
          <button type="button" className="text-gray-500 hover:text-gray-700">
            <HelpCircle className="h-4 w-4" />
          </button>
        </TooltipTrigger>

        {/* Tooltip Content */}
        <TooltipContent side="right" className="max-w-xs py-1">
          <p className="mb-1">Need help finding your credentials?</p>
          <a
            href={DOCS_LINK[type]}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-1 text-blue-600 hover:text-blue-800 hover:underline"
          >
            View documentation
            <ExternalLink className="h-3 w-3" />
          </a>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}
