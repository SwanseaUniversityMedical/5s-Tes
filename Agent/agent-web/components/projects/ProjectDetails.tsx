"use client";

import type { TreProject } from "@/types/TreProject";
import { formatDate } from "date-fns/format";
import { Badge } from "../ui/badge";
import { getDecisionInfo } from "@/types/Decision";
import { Tooltip, TooltipContent, TooltipTrigger } from "../ui/tooltip";

export default function ProjectDetails({ project }: { project: TreProject }) {
  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-center gap-2">
        <Tooltip>
          <TooltipTrigger>
            <Badge variant={getDecisionInfo(project.decision).badgeVariant}>
              <span>{getDecisionInfo(project.decision).label}</span>
            </Badge>
          </TooltipTrigger>
          <TooltipContent>
            {getDecisionInfo(project.decision).label.toLowerCase() ===
            "pending" ? (
              "Waiting for review"
            ) : (
              <>
                {" "}
                <p>
                  {project.lastDecisionDate !== "0001-01-01T00:00:00" && (
                    <>
                      <span>Last decision date: </span>
                      <span>
                        {formatDate(
                          new Date(project.lastDecisionDate),
                          "d MMM yyyy HH:mm",
                        )}
                      </span>
                    </>
                  )}
                </p>
                <p>
                  {project.approvedBy && (
                    <>
                      <span>by </span>
                      <span>
                        {project.approvedBy ? project.approvedBy : "N/A"}
                      </span>
                    </>
                  )}
                </p>
              </>
            )}
          </TooltipContent>
        </Tooltip>
        {/* show archived badge if project is archived */}
        {project.archived && <Badge variant="destructive">Archived</Badge>}

        <Badge variant="info">
          Expries{" "}
          {project.projectExpiryDate
            ? formatDate(
                new Date(project.projectExpiryDate),
                "dd MMM yyyy HH:mm",
              )
            : "No Expiry Date provided"}
        </Badge>
        <Badge variant="info">
          Submission Bucket: {project.submissionBucketTre}
        </Badge>
        {project.localProjectName && (
          <Badge variant="info">Local Name: {project.localProjectName}</Badge>
        )}
      </div>
      <div className="text-sm text-wrap whitespace-pre-wrap text-balance text-pretty text-left">
        <span>{project.description}</span>
      </div>
    </div>
  );
}
