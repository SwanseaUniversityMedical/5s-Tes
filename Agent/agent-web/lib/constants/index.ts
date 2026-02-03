import { env } from "next-runtime-env";

export const helpdeskUrl = env("NEXT_PUBLIC_HELPDESK_URL");

export const keycloakUrl = env("NEXT_PUBLIC_KEYCLOAK_URL");

export const keycloakRealm = env("NEXT_PUBLIC_KEYCLOAK_REALM");
