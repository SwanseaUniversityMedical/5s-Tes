// File to store the environment variables for the application.
// Client components read NEXT_PUBLIC_* at runtime with useEnvContext.
// Bracket access keeps these values from being inlined at build time.
// biome-ignore lint/complexity/useLiteralKeys: avoid Next.js build-time inlining
export const keycloakRealm = process.env["NEXT_PUBLIC_KEYCLOAK_REALM"];

// Env variables used in runtime, not exposed to the client
export const agentApiUrl = process.env.AGENT_API_URL || "http://localhost:8072";

export const betterAuthUrl =
  process.env.BETTER_AUTH_URL || "http://localhost:3000";

export const keycloakUrl = process.env.KEYCLOAK_URL || "";

export const keycloakClientId = process.env.KEYCLOAK_CLIENT_ID || "";

export const keycloakClientSecret = process.env.KEYCLOAK_CLIENT_SECRET || "";
