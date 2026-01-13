"use client";

import { ColumnDef } from "@tanstack/react-table";
import { TreProject } from "@/types/TreProject";
import { getDecisionInfo } from "@/types/Decision";

export const columns: ColumnDef<TreProject>[] = [
  {
    accessorKey: "submissionProjectName",
    header: "Project Name",
    cell: ({ row }) => {
      return <div>{row.original.submissionProjectName}</div>;
    },
  },
  {
    accessorKey: "decision",
    header: "Decision",
    cell: ({ row }) => {
      const decision = row.original.decision;
      const decisionInfo = getDecisionInfo(decision);
      return <div className={decisionInfo.color}>{decisionInfo.label}</div>;
    },
  },
  {
    accessorKey: "lastDecisionDate",
    header: "Last Decision Date",
    cell: ({ row }) => {
      return <div>{row.original.lastDecisionDate}</div>;
    },
  },
  {
    accessorKey: "approvedBy",
    header: "Approved By",
    cell: ({ row }) => {
      return <div>{row.original.approvedBy}</div>;
    },
  },
];
