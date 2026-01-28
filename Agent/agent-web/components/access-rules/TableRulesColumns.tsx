"use client";

import { ColumnDef } from "@tanstack/react-table";
import { RuleColumns, RuleAction } from "@/types/access-rules";
import TableActionButtons from "./TableActionButtons";

/* ----- Types ------ */

type ColumnOptions = {
  onAction?: (action: RuleAction, rule: RuleColumns) => void;
};

type ColumnMeta = {
  headerClassName?: string;
  cellClassName?: string;
};

/* ----- Create Columns for Access Rules Table ------ */

export const createRulesColumns = ({
  onAction,
}: ColumnOptions): ColumnDef<RuleColumns>[] => [
  {
    accessorKey: "description",
    header: "Description",
    enableSorting: true,
  },
  {
    accessorKey: "inputUser",
    header: "Input: user",
    enableSorting: true,
  },
  {
    accessorKey: "inputProject",
    header: "Input: project",
    enableSorting: true,
  },
  {
    accessorKey: "outputTag",
    header: "Output: tag",
    enableSorting: true,
  },
  {
    accessorKey: "outputValue",
    header: "Output: value",
    enableSorting: true,
  },
  {
    accessorKey: "outputEnv",
    header: "Output: env",
    enableSorting: true,
  },
  {
    id: "actions",
    header: "Actions",
    enableSorting: false,
    meta: {
      headerClassName: "text-center",
      cellClassName: "text-center",
    } satisfies ColumnMeta,
    cell: ({ row }) => (
      <div className="flex justify-center">
        <TableActionButtons rule={row.original} onAction={onAction} />
      </div>
    ),
  },
];
