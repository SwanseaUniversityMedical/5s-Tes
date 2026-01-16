"use client";

import { ColumnDef } from "@tanstack/react-table";
import { TreProject } from "@/types/TreProject";
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
          <Button variant="link" className="p-0 font-semibold">
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
      return <div className={decisionInfo.color}>{decisionInfo.label}</div>;
    },
  },
  {
    id: "Reviewed By",
    header: "Reviewed By",
    cell: ({ row }) => {
      return <div>{row.original.approvedBy}</div>;
    },
  },
  {
    id: "Last Decision Date",
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
    header: "",
    id: "actions",
    cell: ({ row }) => {
      // TODO: Add review logic
      return (
        <Link href={`/projects/${row.original.id}`}>
          <Button
            variant="default"
            className="hover:bg-secondary/80 cursor-pointer"
            size="sm"
          >
            Review
          </Button>
        </Link>
      );
    },
  },
];
