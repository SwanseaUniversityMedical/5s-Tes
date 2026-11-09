import { createAuthClient } from "better-auth/react";
import { toast } from "sonner";
import { extractErrorMessage } from "./helpers";

export const { signIn, signOut, useSession } = createAuthClient();

export const handleLogin = async () => {
  try {
    const result = await signIn.social({
      provider: "keycloak",
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
          window.location.href = "/logged-out";
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
