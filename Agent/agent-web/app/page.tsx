import { auth } from "@/lib/auth";
import { headers } from "next/headers";
import { redirect } from "next/navigation";

export default async function Home() {
  const session = await auth.api.getSession({ headers: await headers() });
  if (!session?.user) {
    return redirect("/sign-in");
  } else if (session?.user && !session.user.roles.includes("dare-tre-admin")) {
    return redirect("/forbidden?code=403");
  }

  return redirect("/projects");
}
