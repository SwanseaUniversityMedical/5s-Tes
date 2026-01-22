"use server";

import { isNextRedirectError } from "@/lib/api/helpers";
import request from "@/lib/api/request";
import type { TreProject } from "@/types/TreProject";

const fetchKeys = {
  listProjects: (params: { showOnlyUnprocessed: boolean }) =>
    `Approval/GetAllTreProjects?showOnlyUnprocessed=${params.showOnlyUnprocessed}`,
  getProject: (projectId: string) =>
    `Approval/GetTreProject?projectId=${projectId}`,
  updateProject: () =>
    `Approval/UpdateProjects`,
  updateMembershipDecisions: () =>
    `Approval/UpdateMembershipDecisions`,
};

export async function getProjects(params: {
  showOnlyUnprocessed: boolean;
}): Promise<TreProject[]> {
  return await request<TreProject[]>(fetchKeys.listProjects(params));
}

export async function getProject(projectId: string): Promise<TreProject> {
  return await request<TreProject>(fetchKeys.getProject(projectId));
}

export async function updateProject(project: TreProject): Promise<TreProject> {
  try {
    const response = await request<TreProject>(fetchKeys.updateProject(), {
      method: "POST",
      // send project as an array becasue the backend expects an array of projects
      body: JSON.stringify([project]),
      headers: {
        "Content-Type": "application/json",
      },
    });
   
    return response;
  } catch (error) {
    if (isNextRedirectError(error)) {
      throw error;
    }
    throw error;
  }
}
