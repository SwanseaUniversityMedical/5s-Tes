import { auth } from "@/lib/auth";
import { headers } from "next/headers";
import { getProjects } from "../api/projects";

export default async function Projects() {
  const projects = await getProjects();
  console.log(projects);
  return (
    <div>
      {projects.map((project: any) => (
        <div key={project.id}>{project.submissionProjectName}</div>
      ))}
    </div>
  );
}
