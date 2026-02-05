"use client";

import { Button } from "@/components/ui/button";
import {
  HoverCard,
  HoverCardContent,
  HoverCardTrigger,
} from "@/components/ui/hover-card";
import { Info } from "lucide-react";
import { DecisionInfo } from "@/types/access-rules";

/* ----- Types ------ */

type MetaFieldConfig = {
  label: string;
  key: keyof DecisionInfo;
};

type DecisionMetaPopoverProps = {
  data: DecisionInfo;
};

/* ----- Constants ------ */

const META_FIELDS: MetaFieldConfig[] = [
  { label: "Decision ID", key: "decisionId" },
  { label: "Decision Name", key: "decisionName" },
  { label: "Hit Policy", key: "hitPolicy" },
];

/* ----- Decision Metadata Popover Component ------ */

export default function DecisionMetadataHoverCard({
  data,
}: DecisionMetaPopoverProps) {
  return (
    <HoverCard openDelay={200} closeDelay={100}>
      <HoverCardTrigger asChild>
        <Button variant="outline" size="sm" className="gap-2">
          <Info className="h-4 w-4" />
          Decision Model Info
        </Button>
      </HoverCardTrigger>

      <HoverCardContent side="top" className="w-80" align="center" sideOffset={8}>
        {/* Content */}
        <div className="space-y-3">
          {META_FIELDS.map((field) => (
            <div key={field.key} className="flex justify-between items-center">
              <span className="text-sm font-medium">{field.label}:</span>
              <span className="text-sm text-muted-foreground bg-gray-100 px-2 py-1 rounded">
                {data[field.key]}
              </span>
            </div>
          ))}
        </div>
      </HoverCardContent>
    </HoverCard>
  );
}
