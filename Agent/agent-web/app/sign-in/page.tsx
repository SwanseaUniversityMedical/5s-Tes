import { Button } from "@/components/ui/button";
import { auth } from "@/lib/auth";
import { getSession } from "@/lib/auth-helpers";
import { extractErrorMessage } from "@/lib/helpers";
import Link from "next/link";
import { redirect } from "next/navigation";

// A page purely to navigate a user to Keycloak log in page should Better Auth ask to log in.
export default async function Signin() {
  // Check session without redirecting
  // TODO: handle error here
  const session = await getSession();
  if (session?.user) {
    const userRoles = (session.user as any).roles || [];
    if (userRoles.includes("dare-tre-admin")) {
      // If user is already logged in with correct role, redirect to projects
      redirect("/projects");
    } else {
      // User is logged in but doesn't have the required role
      redirect("/forbidden?code=403");
    }
  }

  // User is not logged in, redirect to KC login page (get from the auth.api.signInWithOAuth2 call)
  try {
    const result = await auth.api.signInWithOAuth2({
      body: {
        providerId: "keycloak",
        callbackURL: process.env.BETTER_AUTH_URL + "/projects",
      },
    });
    if (result) {
      redirect(result.url);
    }
  } catch (error) {
    console.error(error);
    const errorMessage = extractErrorMessage(error);
    return (
      <div className="flex flex-col items-center justify-center h-screen">
        <h1 className="text-2xl font-bold">Error signing in</h1>
        <p className="text-sm my-3 text-gray-500">Details: {errorMessage}</p>
        <Link href="/sign-in">
          <Button>Try again</Button>
        </Link>
      </div>
    );
  }
}
