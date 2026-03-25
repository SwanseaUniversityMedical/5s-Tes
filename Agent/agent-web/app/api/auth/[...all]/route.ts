import { auth } from "@/lib/auth";
import { toNextJsHandler } from "better-auth/next-js";
import { NextRequest, NextResponse } from "next/server";

const handlers = toNextJsHandler(auth);

/**
 * Next.js standalone builds request.url using the server's bind address
 * (e.g. https://0.0.0.0:3000/...) instead of the actual Host header.
 * This breaks OAuth flows behind a reverse proxy because Better Auth
 * derives callback URLs from request.url.
 * Reconstruct the request with the correct external URL from proxy headers.
 */
function withProxyUrl(request: NextRequest): NextRequest {
  const forwardedHost = request.headers.get("x-forwarded-host");
  const forwardedProto = request.headers.get("x-forwarded-proto");

  if (!forwardedHost) return request;

  const original = new URL(request.url);
  const protocol = forwardedProto || "https";
  const fixedUrl = `${protocol}://${forwardedHost}${original.pathname}${original.search}`;

  return new NextRequest(fixedUrl, {
    method: request.method,
    headers: request.headers,
    body: request.body,
  });
}
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
  const proxiedRequest = withProxyUrl(request);
  try {
    logOAuthDebug(proxiedRequest);
    const response = await handler(proxiedRequest);
    return response;
  } catch (error) {
    const errorMessage =
      error instanceof Error ? error.message : "Unknown error";
    const status =
      error instanceof Error && "status" in error
        ? (error.status as number)
        : 500;

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
