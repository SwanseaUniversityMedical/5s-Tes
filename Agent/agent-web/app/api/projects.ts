"use server";

import request from "@/lib/api/request";

const fetchKeys = {
  listProjects: (params: { showOnlyUnprocessed: boolean }) =>
    `Approval/GetAllTreProjects?showOnlyUnprocessed=${params.showOnlyUnprocessed}`,
};

export async function getProjects(params: {
  showOnlyUnprocessed: boolean;
}): Promise<any> {
  return await request(fetchKeys.listProjects(params));
}
