"use client";

import { signIn, useSession } from "@/lib/auth-client";
import { useRouter } from "next/navigation";
import { useEffect } from "react";

// A page purely to navigate a user to Keycloak log in page should Better Auth ask to log in.
export default function Signin() {
  const router = useRouter();
  const { data: session } = useSession();

  useEffect(() => {
    if (!session?.user) {
      void signIn.oauth2({
        providerId: "keycloak",
        callbackURL: window.location.origin + "/",
      });
    } else if (session?.user) {
      void router.push("/");
    }
  }, [session, router]);

  return <div></div>;
}
