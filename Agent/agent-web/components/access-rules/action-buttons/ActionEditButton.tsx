"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { SquarePen } from "lucide-react";
import { RuleColumns } from "@/types/access-rules";
import RuleFormDialog from "../forms/RulesFormDialog";

/* ----- Types ------ */

type ActionEditButtonProps = {
  rule: RuleColumns;
  onSave: (editedRule: RuleColumns) => void;
};

/* ----- Edit Button Component for Access Rules ------ */

export default function ActionEditButton({
  rule,
  onSave,
}: ActionEditButtonProps) {
  const [isOpen, setIsOpen] = useState(false);

  const handleSubmit = (data: RuleColumns) => {
    onSave(data);
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
