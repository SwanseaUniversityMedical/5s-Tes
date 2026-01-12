import { auth } from "@/lib/auth";
import { toNextJsHandler } from "better-auth/next-js";
import { NextRequest, NextResponse } from "next/server";

const handlers = toNextJsHandler(auth);

async function handleRequest(
  handler: (request: NextRequest) => Promise<Response>,
  request: NextRequest
) {
  try {
    const response = await handler(request);
    return response;
  } catch (error) {
    const status =
      error instanceof Error && "status" in error
        ? (error.status as number)
        : 500;

    return NextResponse.json(
      {
        error: "Authentication error",
        message: error instanceof Error ? error.message : "Unknown error",
      },
      { status }
    );
  }
}

export async function GET(request: NextRequest) {
  return handleRequest(handlers.GET, request);
}

export async function POST(request: NextRequest) {
  return handleRequest(handlers.POST, request);
}
