import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { getProjects } from "./api/projects";
import { EmptyState } from "@/components/empty-state";
import { DataTable } from "@/components/data-table";
import { columns } from "./projects/columns";
import { TreProject } from "@/types/TreProject";
import { Metadata } from "next";

interface ProjectsProps {
  searchParams?: Promise<{ showOnlyUnprocessed: boolean }>;
}
export const metadata: Metadata = {
  title: "TRE Admin Approval Dashboard",
  description: "TRE Approval Dashboard",
};

export default async function ProjectsPage(props: ProjectsProps) {
  const searchParams = await props.searchParams;
  const defaultParams = {
    showOnlyUnprocessed: false,
  };
  const combinedParams = { ...defaultParams, ...searchParams };
  let projects: TreProject[] = [];
  // TODO: Add error handling
  try {
    projects = await getProjects(combinedParams);
  } catch (error) {
    console.error(error);
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
            <a href="?showOnlyUnprocessed=false">
              <TabsTrigger value="all">All Projects</TabsTrigger>
            </a>
            <a href="?showOnlyUnprocessed=true">
              <TabsTrigger value="unprocessed">
                Unprocessed Projects
              </TabsTrigger>
            </a>
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
