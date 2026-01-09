import { createAuthClient } from "better-auth/client";
import { genericOAuthClient } from "better-auth/client/plugins";

export const { signIn, signOut, getAccessToken } = createAuthClient({
  plugins: [genericOAuthClient()],
});

export const handleLogin = async () => {
  try {
    await signIn.oauth2({
      providerId: "keycloak",
      callbackURL: process.env.NEXT_PUBLIC_APP_URL + "/",
    });
  } catch (error) {
    console.error("Sign in failed:", error);
    alert(
      `Sign in failed: ${
        error instanceof Error ? error.message : String(error)
      }`
    );
  }
};

export const handleLogout = async () => {
  await signOut({
    fetchOptions: {
      onSuccess: () => {
        window.location.href = "/";
      },
    },
  });
};

export const getAccessTokenFunc = async () => {
  const accessToken = await getAccessToken({
    providerId: "keycloak",
  });
  console.log(accessToken);
};
