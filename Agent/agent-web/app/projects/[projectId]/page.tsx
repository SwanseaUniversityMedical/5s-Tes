import { FetchError } from "@/components/core/fetch-error";
import MembershipApprovalForm from "@/components/projects/MembershipForm";
import { FieldSeparator } from "@/components/ui/field";
import { getMemberships, getProject } from "@/api/projects";
import { authcheck } from "@/lib/auth-helpers";
import type { TreProject } from "@/types/TreProject";
import ProjectApprovalForm from "@/components/projects/ProjectForm";
import ProjectDetails from "@/components/projects/ProjectDetails";
import type { TreMembershipDecision } from "@/types/TreMembershipDecision";

export default async function ApprovalPage(props: {
  params: Promise<{ projectId: string }>;
}) {
  await authcheck("dare-tre-admin");
  const params = await props.params;
  let project: TreProject | null = null;
  let memberships: TreMembershipDecision[] | null = null;
  let fetchError: string | null = null;

  try {
    project = await getProject(params?.projectId);
    memberships = await getMemberships(params?.projectId);
  } catch (error: any) {
    // for redirecting to work
    if (error?.digest?.startsWith("NEXT_REDIRECT")) {
      throw error;
    }
    fetchError =
      error instanceof Error ? error.message : "Failed to load projects";
  }

  // Show error state if fetching failed
  if (fetchError) {
    return <FetchError error={fetchError} />;
  }

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
