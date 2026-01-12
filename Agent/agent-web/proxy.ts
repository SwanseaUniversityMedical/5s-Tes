import { NextRequest, NextResponse } from "next/server";
import { headers } from "next/headers";
import { auth } from "@/lib/auth";

export async function proxy(request: NextRequest) {
  const session = await auth.api.getSession({
    headers: await headers(),
  });

  // THIS IS NOT SECURE! (As the docs of better auth said)
  // TODO: handling auth checks in each page/route as recommended by the docs
  if (!session) {
    // All routes without a session should be redirected to the sign-in page (which will then redirect to Keycloak login page)
    return NextResponse.redirect(new URL("/sign-in", request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    /*
     * Match all paths (including / index page) except for:
     * 1. /api routes
     * 2. /accounts/login
     * 3. /_next (Next.js internals)
     * 4. /sign-in (sign-in page)
     * 5. /logged-out (logged-out page)
     * 6. /_static (inside /public)
     * 7. /logos folder
     * 8. all root files inside /public (e.g. /favicon.ico)
     */
    "/((?!api/|accounts/login|_next/|sign-in|logged-out|_static/|logos|[\\w-]+\\.\\w+).*)",
  ],
};
