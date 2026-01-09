"use client";

import { Button } from "@/components/ui/button";
import { handleLogin, handleLogout } from "@/lib/auth-client";

export function AuthButton({ mode }: { mode: "login" | "logout" }) {
  if (mode === "logout") {
    return <Button onClick={handleLogout}>Logout</Button>;
  } else if (mode === "login") {
    return <Button onClick={handleLogin}>Login with Keycloak</Button>;
  } else {
    return <Button disabled>Loading...</Button>;
  }
}
