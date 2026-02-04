"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Plus } from "lucide-react";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";

import RefreshButton from "./action-buttons/RefreshButton";
import DeployButton from "./action-buttons/DeployButton";
import DecisionMetadataHoverCard from "./DecisionMetadataCard";
import RuleFormDialog from "./forms/RulesFormDialog";
import { DecisionInfo, RuleFormData } from "@/types/access-rules";
import { createAccessRule } from "@/api/access-rules";

/* ----- Types ------ */

type ToolbarProps = {
  decisionInfo: DecisionInfo;
};

/* ----- Toolbar Buttons Component for Access Rules
(Metadata, Refresh, Deploy, Add New Rule) ------ */

export default function ToolbarButtons({ decisionInfo }: ToolbarProps) {
  const [isAddDialogOpen, setIsAddDialogOpen] = useState(false);
  const router = useRouter();

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
      {/* Metadata Hover Card */}
      <DecisionMetadataHoverCard data={decisionInfo} />

      {/* Action Buttons (Refresh & Deploy) */}
      <RefreshButton />
      <DeployButton />

      {/* Add New Rule Button */}
      <Tooltip>
        <TooltipTrigger asChild>
          <Button
            size="sm"
            variant="outline"
            className="
              border-blue-500
              bg-blue-50
              text-blue-600
              hover:bg-blue-100
              hover:border-blue-600
              hover:text-blue-700
            "
            onClick={() => setIsAddDialogOpen(true)}
          >
            <Plus className="h-4 w-4 mr-1.5" />
            Add New Rule
          </Button>
        </TooltipTrigger>
        <TooltipContent>Create a new access rule</TooltipContent>
      </Tooltip>

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
