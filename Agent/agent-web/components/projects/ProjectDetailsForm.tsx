"use client";

import { FieldGroup } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import type { TreProject } from "@/types/TreProject";
import { formatDate } from "date-fns/format";
import { Badge } from "../ui/badge";
import { Save } from "lucide-react";
import { Controller, useForm } from "react-hook-form";
import { toast } from "sonner";
import { Button } from "../ui/button";

type ProjectDetailsFormData = {
  localProjectName: string;
};

export default function ProjectDetailsForm({
  project,
}: {
  project: TreProject;
}) {
  const form = useForm<ProjectDetailsFormData>({
    defaultValues: {
      localProjectName: project.localProjectName ?? "",
    },
  });

  const { isDirty } = form.formState;

  const handleLocalProjectNameSubmit = async (data: ProjectDetailsFormData) => {
    try {
      const localProjectName = data.localProjectName;
      // TODO: Implement API call to update local project name
      // await updateLocalProjectName(project.id, localProjectName);

      toast.success("Local project name updated successfully", {
        description: `Local project name changed to: ${localProjectName}`,
      });
    } catch (error) {
      toast.error("Failed to update local project name", {
        description:
          error instanceof Error ? error.message : "An error occurred",
      });
    }
  };

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
            {isDirty && (
              <Button
                type="button"
                onClick={form.handleSubmit(handleLocalProjectNameSubmit)}
                className="flex gap-2 h-7"
                variant="outline"
              >
                <Save className="w-4 h-4" /> Save
              </Button>
            )}
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
      </FieldGroup>
    </form>
  );
}
