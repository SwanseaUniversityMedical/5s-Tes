"use server";

import { isNextRedirectError } from "@/lib/api/helpers";
import request from "@/lib/api/request";
import type { ActionResult } from "@/types/ActionResult";
import type {
  TreMembershipDecision,
  UpdateMembershipDecisionDto,
} from "@/types/TreMembershipDecision";
import type { TreProject, UpdateProjectDto } from "@/types/TreProject";

const fetchKeys = {
  listProjects: (params: { showOnlyUnprocessed: boolean }) =>
    `Approval/GetAllTreProjects?showOnlyUnprocessed=${params.showOnlyUnprocessed}`,
  getProject: (projectId: string) =>
    `Approval/GetTreProject?projectId=${projectId}`,
  getMemberships: (projectId: string) =>
    `Approval/GetMemberships?projectId=${projectId}`,
  updateProject: () => `Approval/UpdateProjects`,
  updateMembershipDecisions: () => `Approval/UpdateMembershipDecisions`,
};

async function handleRequest<T>(
  requestPromise: Promise<T>,
): Promise<ActionResult<T>> {
  try {
    const data = await requestPromise;
    return { success: true, data };
  } catch (error) {
    if (isNextRedirectError(error)) {
      throw error;
    }
    return {
      success: false,
      error:
        error instanceof Error ? error.message : "An unexpected error occurred",
    };
  }
}

export async function getProjects(params: {
  showOnlyUnprocessed: boolean;
}): Promise<ActionResult<TreProject[]>> {
  return handleRequest(request<TreProject[]>(fetchKeys.listProjects(params)));
}

export async function getProject(
  projectId: string,
): Promise<ActionResult<TreProject>> {
  return handleRequest(request<TreProject>(fetchKeys.getProject(projectId)));
}

export async function getMemberships(
  projectId: string,
): Promise<ActionResult<TreMembershipDecision[]>> {
  return handleRequest(
    request<TreMembershipDecision[]>(fetchKeys.getMemberships(projectId)),
  );
}

export async function updateProject(
  project: UpdateProjectDto,
): Promise<ActionResult<TreProject[]>> {
  return handleRequest(
    request<TreProject[]>(fetchKeys.updateProject(), {
      method: "POST",
      // send project as an array because the backend expects an array of projects
      body: JSON.stringify([project]),
      headers: {
        "Content-Type": "application/json",
      },
    }),
  );
}

export async function updateMembershipDecisions(
  membershipDecisions: UpdateMembershipDecisionDto[],
): Promise<ActionResult<TreMembershipDecision[]>> {
  return handleRequest(
    request<TreMembershipDecision[]>(fetchKeys.updateMembershipDecisions(), {
      method: "POST",
      body: JSON.stringify(membershipDecisions),
      headers: {
        "Content-Type": "application/json",
      },
    }),
  );
}
