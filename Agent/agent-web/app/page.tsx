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

  return (
    <div className="font-sans grid items-center justify-items-center pt-24 p-8">
      <h1 className="text-2xl font-bold">Agent Web UI Application</h1>
    </div>
  );
}
