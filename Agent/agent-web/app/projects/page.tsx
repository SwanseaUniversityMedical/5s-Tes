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
