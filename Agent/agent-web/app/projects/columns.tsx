"use client";

import { ColumnDef } from "@tanstack/react-table";
import { TreProject } from "@/types/TreProject";
import { getDecisionInfo } from "@/types/Decision";
import { format } from "date-fns/format";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";

export const columns: ColumnDef<TreProject>[] = [
  {
    accessorKey: "submissionProjectName",
    header: "Project Name",
    cell: ({ row }) => {
      return <div>{row.original.submissionProjectName}</div>;
    },
  },
  {
    accessorKey: "memberDecisions",
    header: "Memberships",
    cell: ({ row }) => {
      return (
        <Badge variant="secondary">
          {row.original.memberDecisions?.length} Member(s)
        </Badge>
      );
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
      return (
        <div>
          {format(new Date(row.original.lastDecisionDate), "d MMM yyyy HH:mm")}
        </div>
      );
    },
  },
  {
    accessorKey: "approvedBy",
    header: "Approved By",
    cell: ({ row }) => {
      return <div>{row.original.approvedBy}</div>;
    },
  },
  {
    header: "Actions",
    cell: ({ row }) => {
      // TODO: Add review logic
      return (
        <div>
          <Button
            variant="secondary"
            className="hover:bg-secondary/80 cursor-pointer"
            size="sm"
          >
            Review
          </Button>
        </div>
      );
    },
  },
];
