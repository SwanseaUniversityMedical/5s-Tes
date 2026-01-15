"use client";

import { Button } from "@/components/ui/button";
import { signIn, useSession } from "@/lib/auth-client";
import { extractErrorMessage } from "@/lib/helpers";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";

// A page to navigate a user to Keycloak login page
export default function Signin() {
  const router = useRouter();
  const { data: session, isPending, error: sessionError } = useSession();
  const hasInitiatedLogin = useRef(false);
  const [error, setError] = useState<string | null>(null);

  const initiateLogin = async () => {
    setError(null);
    try {
      const result = await signIn.oauth2({
        providerId: "keycloak",
        callbackURL: window.location.origin + "/projects",
      });

      // Check if the result contains an error
      if (result?.error) {
        setError(extractErrorMessage(result.error));
      }
    } catch (err) {
      setError(extractErrorMessage(err));
    }
  };

  const handleRetry = () => {
    hasInitiatedLogin.current = false;
    setError(null);
    initiateLogin();
  };
  // When page loads...
  useEffect(() => {
    // Handle session check error
    if (sessionError) {
      setError(extractErrorMessage(sessionError));
      return;
    }

    // Wait for session check to complete
    if (isPending) return;

    // If user is already logged in
    if (session?.user) {
      const userRoles = (session.user as any).roles || [];
      if (userRoles.includes("dare-tre-admin")) {
        // User has the required role, redirect to projects
        router.replace("/projects");
      } else {
        // User doesn't have the required role
        router.replace("/forbidden?code=403");
      }
      return;
    }

    // User is not logged in - initiate OAuth flow (only once)
    if (!hasInitiatedLogin.current) {
      hasInitiatedLogin.current = true;
      initiateLogin();
    }
  }, [session, isPending, sessionError, router]);

  // Show error state
  if (error) {
    return (
      <div className="flex flex-col items-center justify-center h-screen">
        <h1 className="text-xl font-semibold text-red-600">Sign in failed</h1>
        <p className="text-sm text-gray-500 mt-2 max-w-md text-center">
          {error}
        </p>
        <Button onClick={handleRetry} className="mt-4">
          Try again
        </Button>
      </div>
    );
  }

  // Show loading state
  return (
    <div className="flex flex-col items-center justify-center h-screen">
      <h1 className="text-xl font-semibold">Redirecting to login...</h1>
      <p className="text-sm text-gray-500 mt-2">
        If this page doesn't load, please try again.
      </p>
      <Button onClick={handleRetry} className="mt-4">
        Try again
      </Button>
    </div>
  );
}
