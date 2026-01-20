import {
  Field,
  FieldDescription,
  FieldGroup,
  FieldLabel,
  FieldLegend,
  FieldSeparator,
  FieldSet,
} from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { getDecisionInfo } from "@/types/Decision";
import type { TreProject } from "@/types/TreProject";
import { formatDate } from "date-fns/format";
import { Badge } from "../ui/badge";
import { Separator } from "../ui/separator";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { Label } from "../ui/label";
import { Check, Clock, X } from "lucide-react";
import { columns } from "@/app/projects/[projectId]/columns";
import { DataTable } from "../data-table";

export default function ApprovalForm({ project }: { project: TreProject }) {
  return (
    <div className="w-full ">
      <form>
        <FieldGroup>
          <FieldSet>
            <div>
              <h1 className="text-2xl font-bold">
                Project: {project.submissionProjectName}
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
                    disabled
                  />
                </span>
              </div>
              <div className="text-sm mt-2">
                <span className="text-gray-500 font-semibold">
                  Description:
                </span>{" "}
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
                <span className="font-semibold text-gray-500 ">
                  Expiry Date:
                </span>{" "}
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

            <Separator />

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
                  <span className="text-gray-500"> by </span>
                  <span>
                    <span className="font-semibold">
                      {project.approvedBy ? project.approvedBy : "N/A"}
                    </span>
                  </span>
                  <span>
                    <span className="text-gray-500"> on </span>
                    <span className="font-semibold">
                      {getDecisionInfo(project.decision).label.toLowerCase() ===
                      "pending"
                        ? "N/A"
                        : formatDate(
                            new Date(project.lastDecisionDate),
                            "d MMM yyyy HH:mm",
                          )}
                    </span>
                  </span>
                </div>
              </div>
              <div className="flex items-center gap-2">
                <h1 className="font-semibold">Update Decision:</h1>
                <RadioGroup
                  className="flex flex-row space-x-2"
                  defaultValue={project.decision.toString()}
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
                  {project.decision === 0 && (
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
              </div>
            </FieldSet>
            <Separator />
            <FieldSet>
              <FieldLabel className="text-lg font-bold">
                Membership Decisions
              </FieldLabel>

              <DataTable
                columns={columns}
                data={project.memberDecisions ?? []}
              />
            </FieldSet>
          </FieldSet>
        </FieldGroup>
      </form>
    </div>
  );
}
