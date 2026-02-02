import { publicKeycloakUrl } from "./constants";

/**
 * Extracts a user-friendly error message from various error formats
 */
export function extractErrorMessage(error: unknown): string {
  if (typeof error === "string") {
    return error;
  }

  if (error instanceof Error) {
    return error.message;
  }

  if (error && typeof error === "object") {
    // Handle API error response format: { error: string, message: string }
    if ("message" in error && typeof error.message === "string") {
      return error.message;
    }
    if ("error" in error && typeof error.error === "string") {
      return error.error;
    }
  }

  return "An unexpected error occurred. Please try again.";
}

/**
 * Get Keycloak issuer URL for server-side calls (internal Docker network)
 */
export function getKeycloakIssuer() {
  if (process.env.KEYCLOAK_URL && process.env.KEYCLOAK_REALM) {
    return `${process.env.KEYCLOAK_URL}/realms/${process.env.KEYCLOAK_REALM}`;
  }
  return "";
}

/**
 * Get Keycloak issuer URL for client-side redirects (publicly accessible)
 * Falls back to internal URL if public URL is not set
 */
export function getKeycloakIssuerPublic() {
  const publicUrl = publicKeycloakUrl || process.env.KEYCLOAK_URL;
  if (publicUrl && process.env.KEYCLOAK_REALM) {
    return `${publicUrl}/realms/${process.env.KEYCLOAK_REALM}`;
  }
  return getKeycloakIssuer();
}
