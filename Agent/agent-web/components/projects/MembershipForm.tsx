"use client";

import { useMemo } from "react";
import { FieldGroup, FieldLabel, FieldSet } from "@/components/ui/field";
import type { Decision } from "@/types/Decision";
import { DataTable } from "../data-table";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { Button } from "../ui/button";
import type { TreMembershipDecision } from "@/types/TreMembershipDecision";
import { createMembershipColumns } from "@/app/projects/[projectId]/columns";
import { Check, Loader2, X } from "lucide-react";

export type ApprovalMembershipFormData = {
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

  const { isDirty, isSubmitting } = form.formState;

  const membershipColumns = useMemo(
    () => createMembershipColumns(form),
    [form],
  );

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
        description: `Updated ${membershipDecisions.length} membership decision(s) ${membershipDecisions.map((md) => md.decision).join(", ")}`,
      });
    } catch (error) {
      toast.error("Failed to update membership decisions", {
        description:
          error instanceof Error ? error.message : "An error occurred",
      });
    }
  };

  return (
    <form>
      <FieldGroup>
        <FieldSet>
          <FieldLabel className="text-lg font-bold">
            Membership Decisions
          </FieldLabel>

          <DataTable
            columns={membershipColumns}
            data={membershipDecisions ?? []}
          />

          <div className="flex justify-start gap-2">
            <Button
              type="button"
              onClick={form.handleSubmit(handleMembershipDecisionSubmit)}
              disabled={!isDirty || isSubmitting}
              className="flex gap-2"
            >
              {isSubmitting ? (
                <>
                  <Loader2 className="w-4 h-4 animate-spin" />
                  Updating...
                </>
              ) : (
                <>
                  <Check className="w-4 h-4" /> Update
                </>
              )}
            </Button>
            {isDirty && (
              <Button
                type="button"
                variant="secondary"
                onClick={() => form.reset()}
                className="flex gap-2"
              >
                <X className="w-4 h-4" /> Reset
              </Button>
            )}
          </div>
        </FieldSet>
      </FieldGroup>
    </form>
  );
}
