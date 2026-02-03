import { keycloakRealm, keycloakUrl, publicKeycloakUrl } from "./constants";

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
 * Get Keycloak issuer URL
 */
export function getKeycloakIssuerPublic() {
  if (publicKeycloakUrl && keycloakRealm) {
    if (publicKeycloakUrl.endsWith("/")) {
      return `${publicKeycloakUrl}realms/${keycloakRealm}`;
    }
    return `${publicKeycloakUrl}/realms/${keycloakRealm}`;
  }
  return `http://localhost:8085/realms/${keycloakRealm}`;
}

export function getKeycloakIssuer() {
  if (keycloakUrl && keycloakRealm) {
    if (keycloakUrl.endsWith("/")) {
      return `${keycloakUrl}realms/${keycloakRealm}`;
    }
    return `${keycloakUrl}/realms/${keycloakRealm}`;
  }
  return `http://localhost:8085/realms/${keycloakRealm}`;
}
