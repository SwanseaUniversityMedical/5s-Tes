"use client";

import type { ColumnDef } from "@tanstack/react-table";
import type { TreProject } from "@/types/TreProject";
import { getDecisionInfo } from "@/types/Decision";
import { format } from "date-fns/format";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import Link from "next/link";

export const columns: ColumnDef<TreProject>[] = [
  {
    id: "Project Name",
    header: "Project Name",
    cell: ({ row }) => {
      return (
        <Link href={`/projects/${row.original.id}`}>
          <Button variant="link" className="p-0 font-semibold cursor-pointer">
            {row.original.submissionProjectName}
          </Button>
        </Link>
      );
    },
  },
  {
    id: "Memberships",
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
    id: "Decision",
    header: "Decision",
    cell: ({ row }) => {
      const decision = row.original.decision;
      const decisionInfo = getDecisionInfo(decision);
      return (
        <Badge variant={decisionInfo.badgeVariant}>{decisionInfo.label}</Badge>
      );
    },
  },
  {
    id: "Reviewed By",
    header: "Reviewed By",
    cell: ({ row }) => {
      const { approvedBy, decision } = row.original;
      const decisionInfo = getDecisionInfo(decision);
      return <div>{decisionInfo.label !== "Pending" ? approvedBy : "N/A"}</div>;
    },
  },
  {
    id: "Last Decision Date",
    header: "Last Decision Date",
    cell: ({ row }) => {
      const { lastDecisionDate, decision } = row.original;
      if (!lastDecisionDate) return "N/A";
      const decisionInfo = getDecisionInfo(decision);
      return (
        <div>
          {decisionInfo.label !== "Pending"
            ? format(new Date(lastDecisionDate), "d MMM yyyy HH:mm")
            : "Waiting for review"}
        </div>
      );
    },
  },
  {
    header: "",
    id: "actions",
    cell: ({ row }) => {
      return (
        <Link href={`/projects/${row.original.id}`}>
          <Button variant="default" className="cursor-pointer" size="sm">
            Review
          </Button>
        </Link>
      );
    },
  },
];
