"use client";

import { useMemo } from "react";
import { BaseTable } from "@/components/data-table/BaseTable";
import { DecisionInfo, RuleColumns, RuleAction} from "@/types/access-rules";
import { createRulesColumns } from "./TableRulesColumns";
import { HoverAddRow } from "./action-buttons/AddNewRowButton";
import TopToolbarButtons from "./TopToolbarButtons";

/* ----- Types ------ */

type AccessRulesTableProps = {
  data: RuleColumns[];
  decisionInfo: DecisionInfo;
  onAction?: (action: RuleAction, rule: RuleColumns) => void;
  onAddRule?: (newRule: RuleColumns) => void;
  addNewRowButtonLabel?: string;
  onToolbarAction?: (action: "refresh" | "deploy" | "add") => void;
};

/* ----- Access Rules Table Component ------ */

export default function AccessRulesTable({
  data,
  decisionInfo,
  onAction,
  onAddRule,
  addNewRowButtonLabel = "Add New Rule",
  onToolbarAction,
}: AccessRulesTableProps) {
  const columns = useMemo(() => createRulesColumns({ onAction }), [onAction]);

  // Handle add rule - passes newRule data to parent
  const handleAddRule = (newRule: RuleColumns) => {
    if (onAddRule) {
      onAddRule(newRule);
    }
  };

  return (
    <BaseTable
      data={data}
      columns={columns}
      searchPlaceholder="Search rules..."
      renderFooterRow={(colSpan) => (
        <HoverAddRow
          colSpan={colSpan}
          onAdd={handleAddRule}
          label={addNewRowButtonLabel}
        />
      )}
      renderToolbar={() => (
        <TopToolbarButtons
          onAction={onToolbarAction}
          decisionInfo={decisionInfo}
        />
      )}
    />
  );
}
