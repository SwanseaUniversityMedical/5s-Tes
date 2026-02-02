import { keycloakRealm, publicKeycloakUrl } from "./constants";

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
  if (process.env.KEYCLOAK_URL && keycloakRealm) {
    return `${process.env.KEYCLOAK_URL}/realms/${keycloakRealm}`;
  }
  return "";
}

/**
 * Get Keycloak issuer URL for client-side redirects or client components (publicly accessible)
 */
export function getKeycloakIssuerPublic() {
  if (publicKeycloakUrl && keycloakRealm) {
    return `${publicKeycloakUrl}/realms/${keycloakRealm}`;
  }
  return getKeycloakIssuer();
}
