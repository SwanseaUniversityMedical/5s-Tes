"use client";

import { useMemo } from "react";
import { BaseTable } from "@/components/data-table/BaseTable";
import { DecisionInfo, RuleColumns } from "@/types/access-rules";
import { createRulesColumns } from "./TableRulesColumns";
import { HoverAddRow } from "./action-buttons/AddNewRowButton";
import TopToolbarButtons from "./TopToolbarButtons";

/* ----- Types ------ */

type AccessRulesTableProps = {
  data: RuleColumns[];
  decisionInfo: DecisionInfo;
  addNewRowButtonLabel?: string;
  onToolbarAction?: (action: "refresh" | "deploy" | "add") => void;
};

/* ----- Access Rules Table Component ------ */

export default function AccessRulesTable({
  data,
  decisionInfo,
  addNewRowButtonLabel = "Add New Rule",
  onToolbarAction,
}: AccessRulesTableProps) {
  const columns = useMemo(() => createRulesColumns(), []);

  return (
    <BaseTable
      data={data}
      columns={columns}
      searchPlaceholder="Search rules..."
      renderFooterRow={(colSpan) => (
        <HoverAddRow colSpan={colSpan} label={addNewRowButtonLabel} />
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
