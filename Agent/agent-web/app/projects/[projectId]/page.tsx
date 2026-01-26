import { FetchError } from "@/components/core/fetch-error";
import MembershipApprovalForm from "@/components/projects/MembershipForm";
import { FieldSeparator } from "@/components/ui/field";
import { getMemberships, getProject } from "@/api/projects";
import { authcheck } from "@/lib/auth-helpers";
import ProjectApprovalForm from "@/components/projects/ProjectForm";
import ProjectDetails from "@/components/projects/ProjectDetails";

export default async function ApprovalPage(props: {
  params: Promise<{ projectId: string }>;
}) {
  await authcheck("dare-tre-admin");
  const params = await props.params;
  //  fetch project
  const projectResult = await getProject(params?.projectId);
  if (!projectResult.success) {
    return <FetchError error={projectResult.error} />;
  }
  const project = projectResult.data;
  // fetch memberships
  const membershipsResult = await getMemberships(params?.projectId);
  if (!membershipsResult.success) {
    return <FetchError error={membershipsResult.error} />;
  }
  const memberships = membershipsResult.data;

  return (
    <div className="space-y-2 my-5 mx-auto max-w-7xl">
      {project ? (
        <div className="flex flex-col gap-4">
          <ProjectDetails project={project} />
          <FieldSeparator />
          <ProjectApprovalForm project={project} />
          <FieldSeparator />
          <MembershipApprovalForm membershipDecisions={memberships ?? []} />
        </div>
      ) : (
        <div>No project found</div>
      )}
    </div>
  );
}
