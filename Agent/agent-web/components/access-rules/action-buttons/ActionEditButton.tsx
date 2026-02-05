"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { SquarePen } from "lucide-react";
import { RuleColumns, RuleFormData } from "@/types/access-rules";
import { updateAccessRule } from "@/api/access-rules";
import RuleFormDialog from "../forms/RulesFormDialog";

/* ----- Types ------ */

type ActionEditButtonProps = {
  rule: RuleColumns;
};

/* ----- Edit Button Component for Access Rules ------ */

export default function ActionEditButton({ rule }: ActionEditButtonProps) {
  const [isOpen, setIsOpen] = useState(false);
  const router = useRouter();

  const handleSubmit = async (data: RuleFormData) => {
    if (!rule.ruleId) {
      toast.error("Cannot edit rule: Missing rule ID");
      return;
    }

    const result = await updateAccessRule(rule.ruleId, {
      inputUser: data.inputUser,
      inputProject: data.inputProject,
      inputSubmissionId: rule.inputSubmissionId, // Preserve original submissionId
      outputTag: data.outputTag,
      outputValue: data.outputValue,
      outputEnv: data.outputEnv,
      description: data.description,
    });

    if (result.success) {
      toast.success("Rule updated successfully");
      setIsOpen(false);
      router.refresh(); // Refresh server data
    } else {
      toast.error(`Failed to update rule: ${result.error}`);
    }
  };

  return (
    <>
      {/* Edit Button - Opens edit dialog when clicked */}
      <Button
        variant="ghost"
        size="icon"
        className="
          h-9 w-9
          text-blue-600
          hover:text-blue-700
          hover:bg-blue-50
          hover:border-blue-300
          border
          border-transparent
        "
        onClick={() => setIsOpen(true)}
      >
        <SquarePen className="h-5 w-5" />
      </Button>

      {/* Edit Rule Dialog - Form for editing access rule details */}
      <RuleFormDialog
        isOpen={isOpen}
        onOpenChange={setIsOpen}
        onSubmit={handleSubmit}
        mode="edit"
        initialData={rule}
      />
    </>
  );
}
