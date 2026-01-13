import { betterAuth } from "better-auth";
import { genericOAuth, keycloak } from "better-auth/plugins";

const baseURL =
  process.env.BETTER_AUTH_URL ||
  process.env.NEXT_PUBLIC_APP_URL ||
  "http://localhost:3000";

export const auth = betterAuth({
  baseURL,
  basePath: "/api/auth",
  plugins: [
    genericOAuth({
      config: [
        keycloak({
          clientId: process.env.KEYCLOAK_CLIENT_ID || "",
          clientSecret: process.env.KEYCLOAK_CLIENT_SECRET || "",
          issuer: process.env.KEYCLOAK_ISSUER || "",
        }),
      ],
    }),
  ],
});
