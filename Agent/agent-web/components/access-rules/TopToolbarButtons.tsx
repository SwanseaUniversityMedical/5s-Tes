"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Plus, RefreshCw, Rocket } from "lucide-react";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";

import DecisionMetadataHoverCard from "./DecisionMetadataCard";
import RuleFormDialog from "./forms/RulesFormDialog";
import { DecisionInfo, RuleColumns } from "@/types/access-rules";

/* ----- Types ------ */

type TopToolbarAction = "refresh" | "deploy" | "add";

type ToolbarProps = {
  onAction?: (action: TopToolbarAction) => void;
  onAddRule?: (data: RuleColumns) => void;
  decisionInfo: DecisionInfo;
};

type ToolbarActionConfig = {
  id: TopToolbarAction;
  label: string;
  tooltip: string;
  Icon: React.ComponentType<{ className?: string }>;
  variant?: React.ComponentProps<typeof Button>["variant"];
  className?: string;
};

/* ----- Toolbar Buttons Component Constants ------ */

const BASE_ACTIONS: ToolbarActionConfig[] = [
  {
    id: "refresh",
    label: "Refresh",
    tooltip: "Refresh the rules list",
    Icon: RefreshCw,
    variant: "outline",
    className:
      "border-foreground text-foreground/80 hover:bg-accent hover:text-foreground",
  },
  {
    id: "deploy",
    label: "Deploy",
    tooltip: "Deploy rules to production",
    Icon: Rocket,
    variant: "outline",
    className:
      "border-green-600 bg-green-50 text-green-600 hover:bg-green-100 hover:border-green-600 hover:text-green-700",
  },
  {
    id: "add",
    label: "Add New Rule",
    tooltip: "Create a new access rule",
    Icon: Plus,
    variant: "outline",
    className:
      "border-blue-500 bg-blue-50 text-blue-600 hover:bg-blue-100 hover:border-blue-600 hover:text-blue-700",
  },
];

/* ----- Toolbar Buttons Component for Access Rules
(Metadata, Refresh, Deploy, Add New Rule) ------ */

export default function ToolbarButtons({ onAction, onAddRule, decisionInfo }: ToolbarProps) {
  const [isAddDialogOpen, setIsAddDialogOpen] = useState(false);
  const handleActionClick = (id: TopToolbarAction) => {
    if (id === "add") {
      setIsAddDialogOpen(true);
    }
    onAction?.(id);
  };

  const handleFormSubmit = (data: RuleColumns) => {
    onAddRule?.(data);
    setIsAddDialogOpen(false);
  };

  return (
    <div className="flex items-center gap-2.5">
      <DecisionMetadataHoverCard data={decisionInfo} />

      {BASE_ACTIONS.map(({ id, label, Icon, tooltip, variant, className }) => (
        <Tooltip key={id}>
          <TooltipTrigger asChild>
            <Button
              size="sm"
              variant={variant}
              className={className}
              onClick={() => handleActionClick(id)}
            >
              <Icon className="h-4 w-4 mr-1.5" />
              {label}
            </Button>
          </TooltipTrigger>
          <TooltipContent>{tooltip}</TooltipContent>
        </Tooltip>
      ))}

      {/* Dialog for adding new rules */}
      <RuleFormDialog
        isOpen={isAddDialogOpen}
        onOpenChange={setIsAddDialogOpen}
        onSubmit={handleFormSubmit}
        mode="add"
      />
    </div>
  );
}
