"use server";

import request from "@/lib/api/request";

const fetchKeys = {
  listProjects: () => "Approval/GetAllTreProjects",
};

export async function getProjects(): Promise<any> {
  return await request(fetchKeys.listProjects());
}
