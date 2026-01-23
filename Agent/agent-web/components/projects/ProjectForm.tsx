"use client";

import { FieldGroup, FieldLabel, FieldSet } from "@/components/ui/field";
import { getDecisionInfo } from "@/types/Decision";
import type { Decision } from "@/types/Decision";
import type { TreProject, UpdateProjectDto } from "@/types/TreProject";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { Label } from "../ui/label";
import { Check, Clock, Loader2, X } from "lucide-react";
import { Controller, useForm } from "react-hook-form";
import { toast } from "sonner";
import { Button } from "../ui/button";
import { Input } from "../ui/input";
import { RADIO_OPTIONS } from "@/lib/constants/radio-options";
import { updateProject } from "@/api/projects";

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

  const handleProjectDetailsSubmit = async (data: ProjectApprovalFormData) => {
    const updatedProject: UpdateProjectDto = {
      id: project.id,
      decision: Number(data.projectDecision) as Decision,
      localProjectName: data.localProjectName,
    };
    const result = await updateProject(updatedProject);

    if (!result.success) {
      toast.error(result.error);
      return;
    }
    toast.success("Project details updated successfully");
    // reload the page after 1 second, may have a better way to do this?
    setTimeout(() => {
      window.location.reload();
    }, 1000);
  };
  //  radio options for project decision including Pending if project decision is Pending
  const options = [...RADIO_OPTIONS];
  if (getDecisionInfo(project.decision).label === "Pending") {
    options.push({
      label: "Pending",
      value: "0",
      icon: <Clock className={`${getDecisionInfo(0).color} w-4 h-4`} />,
    });
  }

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
                  {options.map((option) => {
                    return (
                      <div
                        className="flex items-center space-x-2"
                        key={option.value}
                      >
                        <RadioGroupItem
                          id={`project-${option.value}`}
                          value={option.value}
                        />
                        <Label
                          htmlFor={`project-${option.value}`}
                          className="flex items-center gap-2"
                        >
                          {option.label} {option.icon}
                        </Label>
                      </div>
                    );
                  })}
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
