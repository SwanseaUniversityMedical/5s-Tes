import { auth } from "@/lib/auth";
import { toNextJsHandler } from "better-auth/next-js";
import { NextRequest, NextResponse } from "next/server";

const handlers = toNextJsHandler(auth);

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

function logOAuthDebug(request: NextRequest) {
  const url = new URL(request.url);
  const isCallback = url.pathname.includes("/callback/");
  const isSignin = url.pathname.includes("/signin/");

  if (isCallback || isSignin) {
    const cookies = request.headers.get("cookie") || "(none)";
    const hasPkce = cookies.includes("pkce_code_verifier");
    const hasState = cookies.includes("oauth_state") || cookies.includes("state");
    console.log("[AUTH DEBUG]", {
      path: url.pathname,
      type: isSignin ? "SIGNIN" : "CALLBACK",
      requestUrl: request.url,
      host: request.headers.get("host"),
      xForwardedHost: request.headers.get("x-forwarded-host"),
      xForwardedProto: request.headers.get("x-forwarded-proto"),
      hasCode: url.searchParams.has("code"),
      hasStateCookie: hasState,
      hasPkceCookie: hasPkce,
      BETTER_AUTH_URL: process.env.BETTER_AUTH_URL,
      KEYCLOAK_URL: process.env.KEYCLOAK_URL,
    });
  }
}

async function handleRequest(
  handler: (request: NextRequest) => Promise<Response>,
  request: NextRequest
) {
  try {
    logOAuthDebug(request);
    const response = await handler(request);
    return response;
  } catch (error) {
    const errorMessage =
      error instanceof Error ? error.message : "Unknown error";
    const status =
      error instanceof Error && "status" in error
        ? (error.status as number)
        : 500;

    console.error("[AUTH ERROR]", { errorMessage, status, url: request.url });

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
