"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Plus, Rocket } from "lucide-react";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";

import RefreshButton from "./action-buttons/RefreshButton";
import DecisionMetadataHoverCard from "./DecisionMetadataCard";
import RuleFormDialog from "./forms/RulesFormDialog";
import { DecisionInfo, RuleFormData } from "@/types/access-rules";
import { createAccessRule } from "@/api/access-rules";

/* ----- Types ------ */

type TopToolbarAction = "deploy" | "add";

type ToolbarProps = {
  onAction?: (action: TopToolbarAction) => void;
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

export default function ToolbarButtons({ onAction, decisionInfo }: ToolbarProps) {
  const [isAddDialogOpen, setIsAddDialogOpen] = useState(false);
  const router = useRouter();

  const handleActionClick = (id: "deploy" | "add") => {
    if (id === "add") {
      setIsAddDialogOpen(true);
    }
    onAction?.(id);
  };

  const handleFormSubmit = async (data: RuleFormData) => {
    const result = await createAccessRule({
      inputUser: data.inputUser,
      inputProject: data.inputProject,
      outputTag: data.outputTag,
      outputValue: data.outputValue,
      outputEnv: data.outputEnv,
      description: data.description,
    });

    if (result.success) {
      toast.success("Rule created successfully");
      setIsAddDialogOpen(false);
      router.refresh();
    } else {
      toast.error(`Failed to create rule: ${result.error}`);
    }
  };

  return (
    <div className="flex items-center gap-2.5">
      {/* Metadata Hover Card and Refresh Button */}
      <DecisionMetadataHoverCard data={decisionInfo} />
      <RefreshButton />

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
