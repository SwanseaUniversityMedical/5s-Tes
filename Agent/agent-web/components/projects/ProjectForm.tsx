"use client";

import {
  FieldGroup,
  FieldLabel,
  FieldSeparator,
  FieldSet,
} from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { getDecisionInfo } from "@/types/Decision";
import type { Decision } from "@/types/Decision";
import type { TreProject } from "@/types/TreProject";
import { formatDate } from "date-fns/format";
import { Badge } from "../ui/badge";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { Label } from "../ui/label";
import { Check, Clock, FolderKanban, Save, X } from "lucide-react";
import { Controller, useForm } from "react-hook-form";
import { toast } from "sonner";
import { Button } from "../ui/button";

type ProjectApprovalFormData = {
  projectDecision: string;
};

export default function ProjectApprovalForm({
  project,
}: {
  project: TreProject;
}) {
  const form = useForm<ProjectApprovalFormData>({
    defaultValues: {
      projectDecision: project.decision.toString(),
    },
  });

  const { isDirty } = form.formState;

  const handleProjectDecisionSubmit = async (data: ProjectApprovalFormData) => {
    try {
      const projectDecision = Number(data.projectDecision) as Decision;
      // TODO: Implement API call to update project decision
      // await updateProjectDecision(project.id, projectDecision);

      toast.success("Project decision updated successfully", {
        description: `Decision changed to: ${getDecisionInfo(projectDecision).label}`,
      });
    } catch (error) {
      toast.error("Failed to update project decision", {
        description:
          error instanceof Error ? error.message : "An error occurred",
      });
    }
  };
  console.log(project.lastDecisionDate);
  return (
    <form>
      <FieldGroup>
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-bold">
            {project.submissionProjectName}
          </h1>
          <div className="text-sm mt-2 flex items-center gap-2">
            {/* TODO: add tooltip here */}
            <span className="text-gray-500 font-semibold">Local name:</span>{" "}
            <span>
              <Input
                id="local-project-name"
                placeholder={
                  project.localProjectName
                    ? project.localProjectName
                    : "Local project name"
                }
                className="h-7"
                disabled
              />
            </span>
            <Button
              type="button"
              onClick={form.handleSubmit(handleProjectDecisionSubmit)}
              className="flex gap-2 h-7"
              variant="outline"
            >
              <Save className="w-4 h-4" /> Save
            </Button>
          </div>
          <div className="text-sm mt-2">
            <span className="text-gray-500 font-semibold">Description:</span>{" "}
            <span>
              {project.description
                ? project.description
                : "No description provided"}
            </span>
          </div>
          <div className="text-sm mt-2 flex items-center gap-2">
            <span className="text-gray-500 font-semibold">
              Submission Minio Bucket:
            </span>{" "}
            <Badge variant="outline">
              {project.submissionBucketTre
                ? project.submissionBucketTre
                : "No Submission Minio Bucket provided"}
            </Badge>
          </div>
          <div className="text-sm mt-2 flex items-center gap-2">
            <span className="font-semibold text-gray-500 ">Expiry Date:</span>{" "}
            <Badge variant="outline">
              {project.projectExpiryDate
                ? formatDate(
                    new Date(project.projectExpiryDate),
                    "d MMM yyyy HH:mm",
                  )
                : "No Expiry Date provided"}
            </Badge>
          </div>
        </div>

        <FieldSeparator />

        <FieldSet>
          <FieldLabel className="text-lg font-bold">
            Project Decision
          </FieldLabel>

          <div className="flex items-center gap-2">
            <h1 className="font-semibold">Current Status:</h1>
            <div className="flex items-center gap-1 text-sm">
              <span
                className={`${getDecisionInfo(project.decision).color} font-semibold`}
              >
                {getDecisionInfo(project.decision).label.toLowerCase() ===
                "pending"
                  ? "Waiting for review"
                  : getDecisionInfo(project.decision).label}
              </span>
              {project.approvedBy ? (
                <>
                  <span className="text-gray-500"> by </span>
                  <span>{project.approvedBy ? project.approvedBy : "N/A"}</span>
                </>
              ) : null}

              {project.lastDecisionDate !== "0001-01-01T00:00:00" ? (
                <>
                  <span className="text-gray-500"> on </span>
                  <span>
                    {formatDate(
                      new Date(project.lastDecisionDate),
                      "d MMM yyyy HH:mm",
                    )}
                  </span>
                </>
              ) : null}
            </div>
          </div>
          <div className="flex items-center gap-2">
            <h1 className="font-semibold">Update Decision:</h1>
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

            <Button
              type="button"
              onClick={form.handleSubmit(handleProjectDecisionSubmit)}
              disabled={!isDirty}
              className="ml-4 flex gap-2"
            >
              <FolderKanban className="w-4 h-4" /> Save Project Decision
            </Button>
          </div>
        </FieldSet>
      </FieldGroup>
    </form>
  );
}
