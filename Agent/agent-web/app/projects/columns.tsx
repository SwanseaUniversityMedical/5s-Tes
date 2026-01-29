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
    accessorFn: (row) => row.submissionProjectName ?? "",
    cell: ({ row }) => {
      const { submissionProjectName, localProjectName } = row.original;
      return (
        <div className="flex flex-col">
          <Link href={`/projects/${row.original.id}`}>
            <Button variant="link" className="p-0 font-semibold cursor-pointer">
              {submissionProjectName}
            </Button>
          </Link>
          {localProjectName && (
            <span className="text-sm text-gray-500">
              Local Name: {localProjectName}
            </span>
          )}
        </div>
      );
    },
  },
  {
    id: "Memberships",
    header: "Memberships",
    accessorFn: (row) => row.memberDecisions?.length ?? 0,
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
    accessorFn: (row) => row.decision ?? "",
    cell: ({ row }) => {
      const { decision, archived } = row.original;
      const decisionInfo = getDecisionInfo(decision);
      return (
        <div className="flex items-center gap-2">
        <Badge variant={decisionInfo.badgeVariant}>
            {decisionInfo.label}
          </Badge>
          {archived && <Badge variant="destructive">Archived</Badge>}
        </div>
      );
    },
  },
  {
    id: "Reviewed By",
    header: "Reviewed By",
    accessorFn: (row) => row.approvedBy ?? "", // ✅ string sort
    cell: ({ row }) => {
      const { approvedBy, decision } = row.original;
      const decisionInfo = getDecisionInfo(decision);
      return <div>{decisionInfo.label !== "Pending" ? approvedBy : "N/A"}</div>;
    },
  },
  {
    id: "Last Decision Date",
    header: "Last Decision Date",
    accessorFn: (row) =>
      row.lastDecisionDate ? new Date(row.lastDecisionDate).getTime() : -1,
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
    enableSorting: false,
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
