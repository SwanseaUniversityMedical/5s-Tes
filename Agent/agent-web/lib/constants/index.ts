import { env } from "next-runtime-env";


export const accountManagementUrl = env(
    "NEXT_PUBLIC_ACCOUNT_MANAGEMENT_URL"
  );

export const helpdeskUrl = env(
    "NEXT_PUBLIC_HELPDESK_URL"
  );

export const publicKeycloakUrl = env(
    "NEXT_PUBLIC_KEYCLOAK_URL"
  );
