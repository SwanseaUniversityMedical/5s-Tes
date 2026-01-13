import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { FileScan } from "lucide-react";
import { getProjects } from "./api/projects";
import { EmptyState } from "@/components/empty-state";
import { DataTable } from "@/components/data-table";
import { columns } from "./columns";
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
    showOnlyUnprocessed: true,
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
      <div className="flex font-semibold text-xl items-center">
        <FileScan className="mr-2 text-green-700" />
        <h2>Projects</h2>
      </div>

      <div className="my-3">
        <Tabs
          defaultValue={
            (searchParams as any)?.showOnlyUnprocessed
              ? (searchParams as any)?.showOnlyUnprocessed === "true"
                ? "unprocessed"
                : "all"
              : "unprocessed"
          }
        >
          <TabsList className="mb-2">
            <a href="?showOnlyUnprocessed=true" className="h-full">
              <TabsTrigger value="unprocessed">
                Unprocessed Projects
              </TabsTrigger>
            </a>
            <a href="?showOnlyUnprocessed=false" className="h-full">
              <TabsTrigger value="all">All Projects</TabsTrigger>
            </a>
          </TabsList>

          <TabsContent value="unprocessed">
            {projects.length > 0 ? (
              <DataTable columns={columns} data={projects} />
            ) : (
              <EmptyState
                title="No unprocessed projects found"
                description="All projects have been processed or there are no projects yet."
              />
            )}
          </TabsContent>
          <TabsContent value="all">
            {projects.length > 0 ? (
              <DataTable columns={columns} data={projects} />
            ) : (
              <EmptyState
                title="No projects found yet"
                description="All project should appear here."
              />
            )}
          </TabsContent>
        </Tabs>
      </div>
    </div>
  );
}
