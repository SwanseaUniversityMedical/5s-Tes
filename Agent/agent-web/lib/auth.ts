import { betterAuth, type User } from "better-auth";
import { genericOAuth } from "better-auth/plugins";
import { getKeycloakIssuer } from "./helpers";
import {
  betterAuthUrl,
  keycloakClientId,
  keycloakClientSecret,
} from "./constants";

const baseURL = betterAuthUrl;

export const auth = betterAuth({
  baseURL,
  basePath: "/api/auth",
  user: {
    // add additional field (roles) to the user object
    additionalFields: {
      roles: {
        type: "string",
        array: true,
      },
    },
  },
  account: {
    // the user account data (accessToken, idToken, refreshToken, etc.)
    // will be updated on sign in with the latest data from the provider.
    updateAccountOnSignIn: true,
  },
  plugins: [
    genericOAuth({
      config: [
        {
          providerId: "keycloak",
          clientId: keycloakClientId,
          clientSecret: keycloakClientSecret,
          // URL to fetch the provider's OAuth 2.0/OIDC configuration and auto-discover the endpoints for auth
          discoveryUrl: `${getKeycloakIssuer()}/.well-known/openid-configuration`,
          scopes: ["openid"],
          mapProfileToUser: async (profile) => {
            return {
              roles: profile?.realm_access?.roles || [],
            } as Partial<User>;
          },
        },
      ],
    }),
  ],
});
