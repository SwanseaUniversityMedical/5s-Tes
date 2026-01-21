import request from "@/lib/api/request";
import type { TreProject } from "@/types/TreProject";

const fetchKeys = {
  listProjects: (params: { showOnlyUnprocessed: boolean }) =>
    `Approval/GetAllTreProjects?showOnlyUnprocessed=${params.showOnlyUnprocessed}`,
  getProject: (projectId: string) =>
    `Approval/GetTreProject?projectId=${projectId}`,
};

export async function getProjects(params: {
  showOnlyUnprocessed: boolean;
}): Promise<TreProject[]> {
  return await request<TreProject[]>(fetchKeys.listProjects(params));
}

export async function getProject(projectId: string): Promise<TreProject> {
  return await request<TreProject>(fetchKeys.getProject(projectId));
}
