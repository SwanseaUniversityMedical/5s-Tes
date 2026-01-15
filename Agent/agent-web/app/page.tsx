import { authcheck } from "@/lib/auth-helpers";
import { Metadata } from "next";
import { redirect } from "next/navigation";

export const metadata: Metadata = {
  title: "Agent Web UI Application",
  description: "Agent Web UI Application",
};

export default async function Home() {
  // check if user is authenticated and has the required role and then redirect to projects page
  await authcheck("dare-tre-admin");
  return redirect("/projects");
}
