import { FetchError } from "@/components/core/fetch-error";
import ApprovalForm from "@/components/projects/ApprovalForm";
import { getProject } from "@/lib/api/projects";
import { authcheck } from "@/lib/auth-helpers";
import type { TreProject } from "@/types/TreProject";

export default async function ApprovalPage(props: {
  params: Promise<{ projectId: string }>;
}) {
  await authcheck("dare-tre-admin");
  const params = await props.params;
  let project: TreProject | null = null;
  let fetchError: string | null = null;

  try {
    project = await getProject(params?.projectId);
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
    <div className="space-y-2">
      <div className="my-5 mx-auto max-w-7xl">
        {project ? (
          <ApprovalForm project={project} />
        ) : (
          <div>No project found</div>
        )}
      </div>
    </div>
  );
}
