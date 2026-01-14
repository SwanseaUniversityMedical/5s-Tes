import request from "./request";
import { TreProject } from "@/types/TreProject";

const fetchKeys = {
  listProjects: (params: { showOnlyUnprocessed: boolean }) =>
    `Approval/GetAllTreProjects?showOnlyUnprocessed=${params.showOnlyUnprocessed}`,
};

export async function getProjects(params: {
  showOnlyUnprocessed: boolean;
}): Promise<TreProject[]> {
  return await request<TreProject[]>(fetchKeys.listProjects(params));
}
