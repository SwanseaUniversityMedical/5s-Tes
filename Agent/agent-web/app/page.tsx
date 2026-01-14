import { auth } from "@/lib/auth";
import { headers } from "next/headers";
import { AuthButton } from "@/components/auth-button";
import { redirect } from "next/navigation";

export default async function Home() {
  const session = await auth.api.getSession({ headers: await headers() });
  if (!session?.user) {
    return redirect("/sign-in");
  } else if (session?.user && !session.user.roles.includes("dare-tre-admin")) {
    return redirect("/forbidden?code=403");
  }

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
    <div className="font-sans grid grid-rows-[20px_1fr_20px] items-center justify-items-center min-h-screen p-8 pb-20 gap-16 sm:p-20">
      <h1 className="text-2xl font-bold">Agent Web UI Application</h1>
      <div className="flex flex-col items-center gap-4">
        {session?.user && (
          <div className="text-center">
            <p className="text-lg">You are logged in!</p>
            <p className="text-sm text-gray-600 my-3">
              Welcome, {session.user.name || session.user.email}
            </p>
            <AuthButton mode="logout" />
          </div>
        )}
        {!session?.user && (
          <div className="text-center">
            <p className="text-lg">Hello, Guest!</p>
            <AuthButton mode="login" />
          </div>
        )}
      </div>
    </div>
  );
}
