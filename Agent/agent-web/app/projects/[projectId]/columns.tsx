"use client";

import type { ColumnDef } from "@tanstack/react-table";
import { getDecisionInfo } from "@/types/Decision";
import { format } from "date-fns/format";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import Link from "next/link";
import type { TreMembershipDecision } from "@/types/TreMembershipDecision";
import { Clock } from "lucide-react";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { Label } from "@/components/ui/label";
import { type UseFormReturn, Controller } from "react-hook-form";
import type { ApprovalMembershipFormData } from "@/components/projects/MembershipForm";
import { RADIO_OPTIONS } from "@/lib/constants/radio-options";

export const createMembershipColumns = (
  form: UseFormReturn<ApprovalMembershipFormData>,
): ColumnDef<TreMembershipDecision>[] => [
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
      const decision = row.original.decision;
      return (
        <Badge variant={getDecisionInfo(decision).badgeVariant}>
          {getDecisionInfo(decision).label}
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
      const { id, decision } = row.original;
      const baseId = `membership-${id}`;
      const options = [...RADIO_OPTIONS];
      if (getDecisionInfo(decision).label === "Pending") {
        options.push({
          label: "Pending",
          value: "0",
          icon: <Clock className={`${getDecisionInfo(0).color} w-4 h-4`} />,
        });
      }
      return (
        <Controller
          name={`membershipDecisions.${id}` as const}
          control={form.control}
          render={({ field }) => {
            const currentValue =
              field.value ?? row.original.decision.toString();
            return (
              <RadioGroup
                className="flex flex-row space-x-4"
                value={currentValue}
                onValueChange={field.onChange}
              >
                {options.map((option) => (
                  <div
                    className="flex items-center space-x-2"
                    key={option.value}
                  >
                    <RadioGroupItem
                      id={`${baseId}-${option.value}`}
                      value={option.value}
                    />
                    <Label
                      htmlFor={`${baseId}-${option.value}`}
                      className="flex items-center gap-2"
                    >
                      {option.label} {option.icon}
                    </Label>
                  </div>
                ))}
              </RadioGroup>
            );
          }}
        />
      );
    },
  },
];
