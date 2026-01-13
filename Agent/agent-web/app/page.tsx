import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { FileScan } from "lucide-react";
import { EmptyState } from "@/components/empty-state";
import { getProjects } from "./api/projects";
import { Metadata } from "next";

interface ProjectsProps {
  searchParams?: Promise<{ showOnlyUnprocessed: boolean }>;
}

export const metadata: Metadata = {
  title: "Projects",
  description: "Projects page",
};

export default async function Home(props: ProjectsProps) {
  const searchParams = await props.searchParams;
  const defaultParams = {
    showOnlyUnprocessed: true,
  };
  const combinedParams = { ...defaultParams, ...searchParams };
  const projects = await getProjects(combinedParams);

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
              projects.map((project: any) => (
                <div key={project.id}>{project.submissionProjectName}</div>
              ))
            ) : (
              <EmptyState
                title="No unprocessed projects found"
                description="All projects have been processed or there are no projects yet."
              />
            )}
          </TabsContent>
          <TabsContent value="all">
            {projects.length > 0 ? (
              projects.map((project: any) => (
                <div key={project.id}>{project.submissionProjectName}</div>
              ))
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
