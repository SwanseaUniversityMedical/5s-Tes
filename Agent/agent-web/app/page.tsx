import { authcheck } from "@/lib/auth-helpers";
import { redirect } from "next/navigation";

export default async function Home() {
  // check if user is authenticated and has the required role and then redirect to projects page
  await authcheck("dare-tre-admin");
  return redirect("/projects");
}
