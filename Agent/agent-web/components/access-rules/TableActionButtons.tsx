"use client";

import { RuleColumns, RuleAction } from "@/types/access-rules";
import ActionEditButton from "./action-buttons/ActionEditButton";
import ActionDeleteButton from "./action-buttons/ActionDeleteButton";

/* ----- Types ------ */

type TableActionButtonsProps = {
  rule: RuleColumns;
  onAction?: (action: RuleAction, rule: RuleColumns) => void;
};

/* ----- Action Buttons Component (Edit & Delete) for Table Rows ------ */

export default function TableActionButtons({
  rule,
  onAction,
}: TableActionButtonsProps) {
  const handleEdit = (editedRule: RuleColumns) => {
    onAction?.("edit", editedRule);
  };

  const handleDelete = (deletedRule: RuleColumns) => {
    onAction?.("delete", deletedRule);
  };

  return (
    <div className="flex items-center gap-1">
      <ActionEditButton rule={rule} onSave={handleEdit} />
      <ActionDeleteButton rule={rule} onDelete={handleDelete} />
    </div>
  );
}
