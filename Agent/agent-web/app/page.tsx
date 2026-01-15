import { authcheck } from "@/lib/auth-helpers";
import { Metadata } from "next";
import { redirect } from "next/navigation";

export const metadata: Metadata = {
  title: "Agent Web UI Application",
  description: "Agent Web UI Application",
};

  return (
    <div className="font-sans grid items-center justify-items-center pt-24 p-8">
      <h1 className="text-2xl font-bold">Agent Web UI Application</h1>
    </div>
  );
export default async function Home() {
  // check if user is authenticated and has the required role and then redirect to projects page
  await authcheck("dare-tre-admin");
  return redirect("/projects");
}
