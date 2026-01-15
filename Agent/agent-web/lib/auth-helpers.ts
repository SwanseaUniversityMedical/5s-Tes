import { auth } from "./auth";
import { headers } from "next/headers";
import { redirect } from "next/navigation";

/**
 * Get session without redirecting (for sign-in page)
 * @returns The session or null if not authenticated
 */
export async function getSession() {
  const headersList = await headers();
  return await auth.api.getSession({ headers: headersList });
}

/**
 * Check if user is authenticated and has required role
 * Redirects to sign-in if not authenticated, or forbidden if missing role
 * @param requiredRole - The role required to access the page (e.g., "dare-tre-admin")
 * @returns The session if user is authenticated and has the required role
 */
export async function authcheck(requiredRole?: string) {
  const session = await getSession();

  // Check if user is authenticated
  if (!session?.user) {
    redirect("/sign-in");
  }

  // Check if user has required role
  if (requiredRole) {
    const userRoles = (session.user as any).roles || [];
    if (!userRoles.includes(requiredRole)) {
      redirect("/forbidden?code=403");
    }
  }

  return session;
}
