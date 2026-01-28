import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { EmptyState } from "@/components/empty-state";
import { BaseTable } from "@/components/data-table/BaseTable";
import { columns } from "./columns";
import type { Metadata } from "next";
import { authcheck } from "@/lib/auth-helpers";
import { getProjects } from "@/api/projects";
import Link from "next/link";
import { FetchError } from "@/components/core/fetch-error";
import { Button } from "@/components/ui/button";
import { PageHeader } from "@/components/core/page-header";

interface ProjectsProps {
  searchParams?: Promise<{ showOnlyUnprocessed: boolean }>;
}

export const metadata: Metadata = {
  title: "Agent Web UI - Projects",
  description: "List of projects on the connected Submission Layer",
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

  const result = await getProjects(combinedParams);
  if (!result.success) {
    return <FetchError error={result.error} />;
  }
  const projects = result.data;

  const sortedProjects = [...projects].sort((a, b) => a.id - b.id);

  return (
    <>
      <PageHeader
        title="Projects"
        description={
          <>
            List of projects on the connected{" "}
          <a
            href="https://docs.federated-analytics.ac.uk/submission"
            target="_blank"
            rel="noopener noreferrer"
          >
            <Button
              variant="link"
              className="p-0 font-semibold text-md cursor-pointer"
            >
              Submission layer
            </Button>
          </a>
          .
          </>
        }
      />

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
            <TabsTrigger value="unprocessed">Unprocessed Projects</TabsTrigger>
          </Link>
        </TabsList>
        <TabsContent value="all">
          {projects.length > 0 ? (
            <div className="mx-auto max-w-7xl">
              <BaseTable
                columns={columns}
                data={sortedProjects}
                projectListingPage={true}
                searchPlaceholder="Search by project name"
              />
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
              <BaseTable
                columns={columns}
                data={sortedProjects}
                projectListingPage={true}
              />
            </div>
          ) : (
            <EmptyState
              title="No unprocessed projects found"
              description="All projects have been processed or there are no projects yet."
            />
          )}
        </TabsContent>
      </Tabs>
    </>
  );
}
