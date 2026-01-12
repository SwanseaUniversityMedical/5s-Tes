import { createAuthClient } from "better-auth/client";
import { genericOAuthClient } from "better-auth/client/plugins";
import { toast } from "sonner";
import { extractErrorMessage } from "./helpers";

export const { signIn, signOut, getAccessToken } = createAuthClient({
  plugins: [genericOAuthClient()],
});

export const handleLogin = async () => {
  try {
    const result = await signIn.oauth2({
      providerId: "keycloak",
      callbackURL: window.location.origin + "/",
    });

    // Check if the result contains an error
    if (result && result.error) {
      const errorMessage = extractErrorMessage(result.error);
      toast.error(errorMessage);
      return;
    }
  } catch (error) {
    const errorMessage = extractErrorMessage(error);
    toast.error(errorMessage);
  }
};

export const handleLogout = async () => {
  try {
    const result = await signOut({
      fetchOptions: {
        onSuccess: () => {
          toast.success("Logged out successfully");
          window.location.href = "/";
        },
      },
    });

    // Check if the result contains an error
    if (result && "error" in result && result.error) {
      const errorMessage = extractErrorMessage(result.error);
      toast.error(errorMessage);
      return;
    }
  } catch (error) {
    const errorMessage = extractErrorMessage(error);
    toast.error(errorMessage);
  }
};

export const getAccessTokenFunc = async () => {
  const accessToken = await getAccessToken({
    providerId: "keycloak",
  });
  console.log(accessToken);
};
