import { auth } from "@/lib/auth";
import { toNextJsHandler } from "better-auth/next-js";
import { NextRequest, NextResponse } from "next/server";

const handlers = toNextJsHandler(auth);

// Clear all auth-related cookies
function clearAuthCookies(response: NextResponse) {
  response.cookies.set("better-auth.session_token", "", {
    expires: new Date(0),
    path: "/",
  });
  response.cookies.set("better-auth.oauth_state", "", {
    expires: new Date(0),
    path: "/",
  });
}

async function handleRequest(
  handler: (request: NextRequest) => Promise<Response>,
  request: NextRequest
) {
  try {
    const response = await handler(request);
    return response;
  } catch (error) {
    const errorMessage =
      error instanceof Error ? error.message : "Unknown error";
    const status =
      error instanceof Error && "status" in error
        ? (error.status as number)
        : 500;

    // Clear cookies and return error response
    // This allows the client to retry with a clean state
    const response = NextResponse.json(
      {
        error: "Authentication error",
        message: errorMessage,
      },
      { status }
    );
    clearAuthCookies(response);
    return response;
  }
}

export async function GET(request: NextRequest) {
  return handleRequest(handlers.GET, request);
}

export async function POST(request: NextRequest) {
  return handleRequest(handlers.POST, request);
}
