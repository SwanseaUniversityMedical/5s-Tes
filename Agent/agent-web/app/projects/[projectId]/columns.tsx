"use client";

import type { ColumnDef } from "@tanstack/react-table";
import { getDecisionInfo } from "@/types/Decision";
import { format } from "date-fns/format";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import Link from "next/link";
import type { TreMembershipDecision } from "@/types/TreMembershipDecision";
import { Check, Clock, X } from "lucide-react";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { Label } from "@/components/ui/label";

export const columns: ColumnDef<TreMembershipDecision>[] = [
  {
    id: "Username",
    header: "Username",
    cell: ({ row }) => {
      return (
        <Link href={`/projects/${row.original.id}`}>
          <Button variant="link" className="p-0 font-semibold cursor-pointer">
            {row.original.user.username}
          </Button>
        </Link>
      );
    },
  },
  {
    id: "Status",
    header: "Status",
    cell: ({ row }) => {
      return (
        <Badge
          variant="outline"
          className={getDecisionInfo(row.original.decision).color}
        >
          {getDecisionInfo(row.original.decision).label}
        </Badge>
      );
    },
  },
  {
    id: "Last Decision Date",
    header: "Last Decision Date",
    cell: ({ row }) => {
      const decision = row.original.decision;
      return getDecisionInfo(decision).label !== "Pending" ? (
        <div className="flex items-center gap-1 text-sm">
          <span>
            {format(
              new Date(row.original.lastDecisionDate),
              "d MMM yyyy HH:mm",
            )}
          </span>
          <span>by</span>
          <span>{row.original.approvedBy}</span>
        </div>
      ) : (
        <span>Waiting for review</span>
      );
    },
  },
  {
    id: "Update Decision",
    header: "Update Decision",
    cell: ({ row }) => {
      const decision = row.original.decision;

      return (
        <RadioGroup
          className="flex flex-row space-x-4"
          defaultValue={decision.toString()}
        >
          <div className="flex items-center space-x-2">
            <RadioGroupItem id="approve" value="1" />
            <Label htmlFor="approve" className="flex items-center gap-2">
              Approve{" "}
              <Check className={`${getDecisionInfo(1).color} w-4 h-4`} />
            </Label>
          </div>
          <div className="flex items-center space-x-2">
            <RadioGroupItem id="reject" value="2" />
            <Label htmlFor="reject" className="flex items-center gap-2">
              Reject <X className={`${getDecisionInfo(2).color} w-4 h-4`} />
            </Label>
          </div>
          {decision === 0 && (
            <div className="flex items-center space-x-2">
              <RadioGroupItem id="pending" value="0" />
              <Label htmlFor="pending" className="flex items-center gap-2">
                Pending{" "}
                <Clock className={`${getDecisionInfo(0).color} w-4 h-4`} />
              </Label>
            </div>
          )}
        </RadioGroup>
      );
    },
  },
];
