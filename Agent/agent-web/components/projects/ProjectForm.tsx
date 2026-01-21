"use client";

import { FieldGroup, FieldLabel, FieldSet } from "@/components/ui/field";
import { getDecisionInfo } from "@/types/Decision";
import type { Decision } from "@/types/Decision";
import type { TreProject } from "@/types/TreProject";
import { formatDate } from "date-fns/format";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { Label } from "../ui/label";
import { Check, Clock, FolderKanban, Loader2, Save, X } from "lucide-react";
import { Controller, useForm } from "react-hook-form";
import { toast } from "sonner";
import { Button } from "../ui/button";
import { Input } from "../ui/input";

type ProjectApprovalFormData = {
  projectDecision: string;
  localProjectName: string;
};

export default function ProjectApprovalForm({
  project,
}: {
  project: TreProject;
}) {
  const form = useForm<ProjectApprovalFormData>({
    defaultValues: {
      projectDecision: project.decision.toString(),
      localProjectName: project.localProjectName ?? "",
    },
  });

  const { isDirty, isSubmitting } = form.formState;
  console.log(isDirty, isSubmitting);

  const handleProjectDetailsSubmit = async (data: ProjectApprovalFormData) => {
    try {
      const projectDecision = Number(data.projectDecision) as Decision;
      // TODO: Implement API call to update project decision
      // await updateProjectDecision(project.id, projectDecision);

      toast.success("Project decision updated successfully", {
        description: `Decision changed to: ${getDecisionInfo(projectDecision).label} and local name changed to: ${data.localProjectName}`,
      });
    } catch (error) {
      toast.error("Failed to update project decision", {
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
            Update Project Details
          </FieldLabel>
          <div className="flex items-center gap-2">
            {/* TODO: add tooltip here */}
            <span className="text-sm font-semibold">Local name:</span>{" "}
            <span>
              <Controller
                name="localProjectName"
                control={form.control}
                render={({ field }) => (
                  <Input
                    id="local-project-name"
                    className="h-7"
                    value={field.value}
                    onChange={field.onChange}
                  />
                )}
              />
            </span>
          </div>

          <div className="flex items-center gap-2">
            <h1 className="text-sm font-semibold"> Decision:</h1>
            <Controller
              name="projectDecision"
              control={form.control}
              render={({ field }) => (
                <RadioGroup
                  className="flex flex-row space-x-2"
                  value={field.value}
                  onValueChange={field.onChange}
                >
                  <div className="flex items-center space-x-2">
                    <RadioGroupItem id="project-approve" value="1" />
                    <Label
                      htmlFor="project-approve"
                      className="flex items-center gap-2"
                    >
                      Approve{" "}
                      <Check
                        className={`${getDecisionInfo(1).color} w-4 h-4`}
                      />
                    </Label>
                  </div>
                  <div className="flex items-center space-x-2">
                    <RadioGroupItem id="project-reject" value="2" />
                    <Label
                      htmlFor="project-reject"
                      className="flex items-center gap-2"
                    >
                      Reject{" "}
                      <X className={`${getDecisionInfo(2).color} w-4 h-4`} />
                    </Label>
                  </div>
                  {getDecisionInfo(project.decision).label === "Pending" && (
                    <div className="flex items-center space-x-2">
                      <RadioGroupItem id="project-pending" value="0" />
                      <Label
                        htmlFor="project-pending"
                        className="flex items-center gap-2"
                      >
                        Pending{" "}
                        <Clock
                          className={`${getDecisionInfo(0).color} w-4 h-4`}
                        />
                      </Label>
                    </div>
                  )}
                </RadioGroup>
              )}
            />
          </div>

          <div className="flex gap-2 justify-start">
            <Button
              type="button"
              onClick={form.handleSubmit(handleProjectDetailsSubmit)}
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
