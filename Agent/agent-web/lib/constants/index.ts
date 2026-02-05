// File to store the environment variables for the application
import { env } from "next-runtime-env";

// Env variables for the application, exposed to the client, and can be used in runtime thanks to next-runtime-env
export const helpdeskUrl = env("NEXT_PUBLIC_HELPDESK_URL") || "#";

export const publicKeycloakUrl = env("NEXT_PUBLIC_KEYCLOAK_URL");

export const keycloakRealm = env("NEXT_PUBLIC_KEYCLOAK_REALM");

// Env variables used in runtime, not exposed to the client
export const agentApiUrl = process.env.AGENT_API_URL || "http://localhost:8072";

export const betterAuthUrl =
  process.env.BETTER_AUTH_URL || "http://localhost:3000";

export const keycloakUrl = process.env.KEYCLOAK_URL || "";

export const keycloakClientId = process.env.KEYCLOAK_CLIENT_ID || "";

export const keycloakClientSecret = process.env.KEYCLOAK_CLIENT_SECRET || "";
