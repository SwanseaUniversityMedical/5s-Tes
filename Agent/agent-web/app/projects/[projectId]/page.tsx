import { FetchError } from "@/components/core/fetch-error";
import MembershipApprovalForm from "@/components/projects/MembershipForm";
import { FieldSeparator } from "@/components/ui/field";
import { getMemberships, getProject } from "@/api/projects";
import { authcheck } from "@/lib/auth-helpers";
import ProjectApprovalForm from "@/components/projects/ProjectForm";
import ProjectDetails from "@/components/projects/ProjectDetails";
import type { Metadata } from "next";
import { PageHeader } from "@/components/core/page-header";
import { Button } from "@/components/ui/button";

export const metadata: Metadata = {
  title: "Agent Web UI - Project and Memberships Approval",
  description:
    "Review project details and approve/reject the project and its memberships.",
};

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
    <>
      <PageHeader
        title={project.submissionProjectName ?? "Project"}
        description={<>
          Review project details and { " "} 
          <a
            href="https://docs.federated-analytics.ac.uk/submission/tasks/run_analysis#update-the-dmn-rules"
            target="_blank"
            rel="noopener noreferrer"
          >
            <Button
              variant="link"
              className="p-0 font-semibold text-md cursor-pointer"
            >
              approve/reject the project 
            </Button>
          </a>{" "}
          and its memberships.
        </>}
      />

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
    </>
  );
}
