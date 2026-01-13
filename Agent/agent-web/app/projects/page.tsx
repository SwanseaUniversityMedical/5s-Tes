import { auth } from "@/lib/auth";
import { headers } from "next/headers";

export default async function Projects() {
  const accessToken = await auth.api.getAccessToken({
    body: {
      providerId: "keycloak",
    },
    headers: await headers(),
  });

  const data = await fetch(
    "http://localhost:8072/api/Approval/GetAllTreProjects",
    {
      headers: {
        Authorization: `Bearer ${accessToken.accessToken}`,
        "Content-Type": "application/json",
      },
      method: "GET",
    }
  );
  const projects = await data.json();

  return (
    <div>
      {projects.map((project: any) => (
        <div key={project.id}>{project.submissionProjectName}</div>
      ))}
    </div>
  );
}
