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

const request = async <T>(url: string, options: RequestOptions = {}) => {
  const baseUrl = options.baseUrl ?? `${agentApiUrl}/api`;

  let requestHeaders: Record<string, string> = { ...(options.headers || {}) };
  try {
    const accessToken = await auth.api.getAccessToken({
      body: {
        providerId: "keycloak",
      },
      headers: await headers(),
    });

    if (accessToken.accessToken) {
      requestHeaders.Authorization = `Bearer ${accessToken.accessToken}`;
    }
  } catch (error) {
    // redirect to the token-expired page, cause the access token maybe not valid anymore.
    redirect("/token-expired");
  }
  const response = await fetch(`${baseUrl}/${url}`, {
    method: options.method || "GET",
    headers: requestHeaders,
    body: options.body,
    cache: options.cache,
    next: options.next,
  });

  const contentType = response.headers.get("Content-Type");
  // TODO: add error handling for the response
  // Maybe we have to get the json response, extract the error message and return it as a string
  if (!response.ok) {
    // TODO: const errorResponse = await response.json();
    let errorMessage = "An error occurred";
    if (
      contentType &&
      (contentType.includes("application/json") ||
        contentType?.includes("application/problem+json"))
    ) {
      try {
        const errorResponse = await response.json();
        if (Array.isArray(errorResponse)) {
          errorMessage = errorResponse.join(" * ");
        } else {
          errorMessage = errorResponse.detail || errorMessage;
        }
      } catch (error) {
        errorMessage = "Failed to parse error response";
      }
    }
    throw new Error(errorMessage);
  }

  if (response.status === 204) {
    return {} as T;
  }

  if (contentType && contentType.includes("application/json")) {
    const data = await response.json();
    return resolveJsonReferences(data);
  }
  return response.text();
};

export default request;
