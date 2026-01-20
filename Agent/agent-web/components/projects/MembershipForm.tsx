"use client";

import { useMemo } from "react";
import { FieldGroup, FieldLabel, FieldSet } from "@/components/ui/field";
import type { Decision } from "@/types/Decision";
import { DataTable } from "../data-table";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { Button } from "../ui/button";
import { columns } from "@/app/projects/[projectId]/columns";
import type { TreMembershipDecision } from "@/types/TreMembershipDecision";

type ApprovalMembershipFormData = {
  membershipDecisions: Record<string, string>;
};

export default function MembershipApprovalForm({
  membershipDecisions,
}: {
  membershipDecisions: TreMembershipDecision[];
}) {
  const defaultMembershipDecisions = useMemo(() => {
    const decisions: Record<string, string> = {};
    (membershipDecisions ?? []).forEach((md) => {
      decisions[md.id.toString()] = md.decision.toString();
    });
    return decisions;
  }, [membershipDecisions]);

  const form = useForm<ApprovalMembershipFormData>({
    defaultValues: {
      membershipDecisions: defaultMembershipDecisions,
    },
  });

  const handleMembershipDecisionSubmit = async (
    data: ApprovalMembershipFormData,
  ) => {
    try {
      const membershipDecisions = Object.entries(data.membershipDecisions).map(
        ([membershipId, decision]) => ({
          membershipId: Number(membershipId),
          decision: Number(decision) as Decision,
        }),
      );

      // TODO: Implement API call to update membership decisions
      // await updateMembershipDecisions(membershipDecisions);

      toast.success("Membership decisions updated successfully", {
        description: `Updated ${membershipDecisions.length} membership decision(s)`,
      });
    } catch (error) {
      toast.error("Failed to update membership decisions", {
        description:
          error instanceof Error ? error.message : "An error occurred",
      });
    }
  };

  const membershipDecisionsValue = form.watch("membershipDecisions");

  const hasMembershipDecisionsChanged = useMemo(() => {
    for (const md of membershipDecisions ?? []) {
      const currentValue = membershipDecisionsValue[md.id.toString()];
      if (currentValue !== md.decision.toString()) {
        return true;
      }
    }
    return false;
  }, [membershipDecisionsValue, membershipDecisions]);

  return (
    <form>
      <FieldGroup>
        <FieldSet>
          <FieldLabel className="text-lg font-bold">
            Membership Decisions
          </FieldLabel>

          <DataTable columns={columns} data={membershipDecisions ?? []} />
          <div className="flex justify-end mt-4">
            <Button
              type="button"
              onClick={form.handleSubmit(handleMembershipDecisionSubmit)}
              disabled={!hasMembershipDecisionsChanged}
            >
              Save Membership Decisions
            </Button>
          </div>
        </FieldSet>
      </FieldGroup>
    </form>
  );
}
