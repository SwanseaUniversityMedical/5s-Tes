import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { EmptyState } from "@/components/empty-state";
import { DataTable } from "@/components/data-table";
import { columns } from "./columns";
import { TreProject } from "@/types/TreProject";
import { Metadata } from "next";
import { authcheck } from "@/lib/auth-helpers";
import { getProjects } from "@/api/projects";
import Link from "next/link";
import { FetchError } from "@/components/core/fetch-error";

interface ProjectsProps {
  searchParams?: Promise<{ showOnlyUnprocessed: boolean }>;
}
export const metadata: Metadata = {
  title: "TRE Admin Approval Dashboard",
  description: "TRE Approval Dashboard",
};

export default async function ProjectsPage(props: ProjectsProps) {
  // check if user is authenticated and has the required role
  // This will redirect to /sign-in if not authenticated or if session is invalid
  await authcheck("dare-tre-admin");

  // page logics
  const searchParams = await props.searchParams;
  const defaultParams = {
    showOnlyUnprocessed: false,
  };
  const combinedParams = { ...defaultParams, ...searchParams };
  let projects: TreProject[] = [];
  let fetchError: string | null = null;

  try {
    projects = await getProjects(combinedParams);
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
      <div className="my-5 mx-auto max-w-7xl font-bold text-2xl items-center">
        <h2>Projects</h2>
      </div>
      <div className="my-5 mx-auto max-w-7xl">
        <Tabs
          defaultValue={
            (searchParams as any)?.showOnlyUnprocessed
              ? (searchParams as any)?.showOnlyUnprocessed === "true"
                ? "unprocessed"
                : "all"
              : "all"
          }
        >
          <TabsList className="mb-2">
            <Link href="?showOnlyUnprocessed=false" scroll={false}>
              <TabsTrigger value="all">All Projects</TabsTrigger>
            </Link>

            <Link href="?showOnlyUnprocessed=true" scroll={false}>
              <TabsTrigger value="unprocessed">
                Unprocessed Projects
              </TabsTrigger>
            </Link>
          </TabsList>
          <TabsContent value="all">
            {projects.length > 0 ? (
              <div className="mx-auto max-w-7xl">
                <DataTable columns={columns} data={projects} />
              </div>
            ) : (
              <EmptyState
                title="No projects found yet"
                description="All project should appear here."
              />
            )}
          </TabsContent>
          <TabsContent value="unprocessed">
            {projects.length > 0 ? (
              <div className="mx-auto max-w-7xl">
                <DataTable columns={columns} data={projects} />
              </div>
            ) : (
              <EmptyState
                title="No unprocessed projects found"
                description="All projects have been processed or there are no projects yet."
              />
            )}
          </TabsContent>
        </Tabs>
      </div>
    </div>
  );
}
