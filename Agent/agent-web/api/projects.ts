"use server";

import { isNextRedirectError } from "@/lib/api/helpers";
import request from "@/lib/api/request";
import type { ActionResult } from "@/types/ActionResult";
import type {
  TreMembershipDecision,
  UpdateMembershipDecisionDto,
} from "@/types/TreMembershipDecision";
import type { TreProject } from "@/types/TreProject";

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

export async function getProjects(params: {
  showOnlyUnprocessed: boolean;
}): Promise<ActionResult<TreProject[]>> {
  try {
    const response = await request<TreProject[]>(
      fetchKeys.listProjects(params),
    );
    return { success: true, data: response };
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

export async function getProject(
  projectId: string,
): Promise<ActionResult<TreProject>> {
  try {
    const response = await request<TreProject>(fetchKeys.getProject(projectId));
    return { success: true, data: response };
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

export async function getMemberships(
  projectId: string,
): Promise<ActionResult<TreMembershipDecision[]>> {
  try {
    const response = await request<TreMembershipDecision[]>(
      fetchKeys.getMemberships(projectId),
    );
    return { success: true, data: response };
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

export async function updateProject(
  project: TreProject,
): Promise<ActionResult<TreProject[]>> {
  try {
    const response = await request<TreProject[]>(fetchKeys.updateProject(), {
      method: "POST",
      // send project as an array becasue the backend expects an array of projects
      body: JSON.stringify([project]),
      headers: {
        "Content-Type": "application/json",
      },
    });

    return { success: true, data: response as TreProject[] };
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

export async function updateMembershipDecisions(
  membershipDecisions: UpdateMembershipDecisionDto[],
): Promise<ActionResult<TreMembershipDecision[]>> {
  try {
    const response = await request<TreMembershipDecision[]>(
      fetchKeys.updateMembershipDecisions(),
      {
        method: "POST",
        body: JSON.stringify(membershipDecisions),
        headers: {
          "Content-Type": "application/json",
        },
      },
    );

    return { success: true, data: response };
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
