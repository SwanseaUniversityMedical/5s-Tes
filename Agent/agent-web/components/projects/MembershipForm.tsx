"use client";

import { useMemo } from "react";
import { FieldGroup, FieldLabel, FieldSet } from "@/components/ui/field";
import type { Decision } from "@/types/Decision";
import { DataTable } from "../data-table";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { Button } from "../ui/button";
import type {
  TreMembershipDecision,
  UpdateMembershipDecisionDto,
} from "@/types/TreMembershipDecision";
import { createMembershipColumns } from "@/app/projects/[projectId]/columns";
import { Check, Loader2, X } from "lucide-react";
import { updateMembershipDecisions } from "@/api/projects";

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
      // Find membership decisions that have changed and create updated objects
      const updatedMembershipDecisions: UpdateMembershipDecisionDto[] = [];

      for (const [membershipId, newDecision] of Object.entries(
        data.membershipDecisions,
      )) {
        const originalDecision = membershipDecisions.find(
          (md) => md.id.toString() === membershipId,
        );

        if (!originalDecision) {
          continue; // Skip if original not found
        }

        const newDecisionValue = Number(newDecision) as Decision;

        // Only include if the decision has actually changed
        if (originalDecision.decision !== newDecisionValue) {
          updatedMembershipDecisions.push({
            id: originalDecision.id,
            decision: newDecisionValue,
          });
        }
      }

      // Only call API if there are actual changes
      if (updatedMembershipDecisions.length === 0) {
        toast.info("No changes to save");
        return;
      }
      const result = await updateMembershipDecisions(
        updatedMembershipDecisions,
      );
      if (!result.success) {
        toast.error(result.error);
        return;
      }

      toast.success("Membership decisions updated successfully");
      window.location.reload();
    } catch (error) {
      toast.error("Failed to update membership decisions", {
        description:
          error instanceof Error
            ? error.message
            : "An unexpected error occurred",
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
