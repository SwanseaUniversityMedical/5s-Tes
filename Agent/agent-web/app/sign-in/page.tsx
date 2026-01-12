"use client";

import { signIn } from "@/lib/auth-client";
import { useEffect } from "react";

export default function Signin() {
  useEffect(() => {
    void signIn.oauth2({
      providerId: "keycloak",
      callbackURL: window.location.origin + "/",
    });
  }, []);

  return <div></div>;
}
