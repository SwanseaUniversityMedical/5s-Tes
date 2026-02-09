import { redirect } from "next/navigation";
import { auth } from "../auth";
import { headers } from "next/headers";
import { resolveJsonReferences } from "./helpers";
import { agentApiUrl } from "../constants";

interface RequestOptions {
  method?: string;
  headers?: Record<string, string>;
  body?: BodyInit;
  download?: boolean;
  cache?: RequestCache;
  next?: { revalidate: number };
  baseUrl?: string;
}

const getAccessTokenHeader = async (): Promise<string | null> => {
  try {
    const accessToken = await auth.api.getAccessToken({
      body: {
        providerId: "keycloak",
      },
      headers: await headers(),
    });

    return accessToken.accessToken ? `Bearer ${accessToken.accessToken}` : null;
  } catch {
    // redirect to the token-expired page, cause the access token maybe not valid anymore.
    redirect("/token-expired");
  }
};

const parseJsonError = (errorResponse: unknown): string => {
  if (Array.isArray(errorResponse)) {
    return errorResponse.join(" * ");
  }

  if (typeof errorResponse === "object" && errorResponse !== null) {
    const error = errorResponse as Record<string, unknown>;
    return (error.detail ||
      error.message ||
      error.title ||
      "An error occurred") as string;
  }

  return "An error occurred";
};

const extractErrorMessage = async (
  response: Response,
  contentType: string | null,
): Promise<string> => {
  const isJsonContent =
    contentType &&
    (contentType.includes("application/json") ||
      contentType.includes("application/problem+json"));

  if (isJsonContent) {
    try {
      const errorResponse = await response.json();
      return parseJsonError(errorResponse);
    } catch {
      return "Failed to parse error response";
    }
  }

  try {
    const textBody = await response.text();
    return textBody ? textBody.substring(0, 200) : "An error occurred";
  } catch {
    return "An error occurred";
  }
};

const request = async <T>(url: string, options: RequestOptions = {}) => {
  const baseUrl = options.baseUrl ?? `${agentApiUrl}/api`;

  const requestHeaders: Record<string, string> = { ...(options.headers || {}) };
  const authHeader = await getAccessTokenHeader();
  if (authHeader) {
    requestHeaders.Authorization = authHeader;
  }

  const response = await fetch(`${baseUrl}/${url}`, {
    method: options.method || "GET",
    headers: requestHeaders,
    body: options.body,
    cache: options.cache,
    next: options.next,
  });

  const contentType = response.headers.get("Content-Type");

  if (!response.ok) {
    const errorMessage = await extractErrorMessage(response, contentType);
    throw new Error(errorMessage);
  }

  if (response.status === 204) {
    return {} as T;
  }

  if (contentType?.includes("application/json")) {
    const data = await response.json();
    return resolveJsonReferences(data);
  }

  return response.text();
};

export default request;
