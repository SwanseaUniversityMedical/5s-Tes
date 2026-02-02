import { betterAuth, User } from "better-auth";
import { genericOAuth } from "better-auth/plugins";
import { getKeycloakIssuer, getKeycloakIssuerPublic } from "./helpers";

const baseURL =
  process.env.BETTER_AUTH_URL ||
  "http://localhost:3000";

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
          clientId: process.env.KEYCLOAK_CLIENT_ID || "",
          clientSecret: process.env.KEYCLOAK_CLIENT_SECRET || "",
          // Keycloak OAuth2 endpoints
          // authorizationUrl must be publicly accessible (browser redirect)
          authorizationUrl: `${getKeycloakIssuerPublic()}/protocol/openid-connect/auth`,
          // tokenUrl and userInfoUrl are called server-side, can use internal URL
          tokenUrl: `${getKeycloakIssuer()}/protocol/openid-connect/token`,
          userInfoUrl: `${getKeycloakIssuer()}/protocol/openid-connect/userinfo`,
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
