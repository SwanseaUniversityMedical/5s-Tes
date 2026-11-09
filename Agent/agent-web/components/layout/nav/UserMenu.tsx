"use client";

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Button } from "@/components/ui/button";
import { ChevronDown, User } from "lucide-react";
import { handleLogin, handleLogout, useSession } from "@/lib/auth-client";
import { getKeycloakIssuerPublic } from "@/lib/helpers";
import { useEnvContext } from "next-runtime-env";

// Creates User Menu Dropdown button Component in the Navbar

export default function UserMenu() {
  const { data: session, error: sessionError } = useSession();
  const {
    NEXT_PUBLIC_HELPDESK_URL,
    NEXT_PUBLIC_KEYCLOAK_URL,
    NEXT_PUBLIC_KEYCLOAK_REALM,
  } = useEnvContext();
  const username = session?.user?.name;
  const accountHref = `${getKeycloakIssuerPublic(
    NEXT_PUBLIC_KEYCLOAK_URL,
    NEXT_PUBLIC_KEYCLOAK_REALM,
  )}/account`;
  const menuItems = [
    { label: "Account", href: accountHref },
    { label: "Helpdesk", href: NEXT_PUBLIC_HELPDESK_URL || "#" },
  ];

  return (
    <DropdownMenu>
      {/* ---- User Menu Button ---- */}
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          className="inline-flex h-8 items-center gap-2 px-3 text-sm font-normal"
        >
          <User className="h-4 w-4 opacity-80" />
          <span className="max-w-35px truncate text-foreground">
            {username ?? "Guest"}
          </span>
          <ChevronDown className="h-4 w-4 opacity-60" />
        </Button>
      </DropdownMenuTrigger>

      {/* ---- Dropdown Menu Trigger ---- */}
      <DropdownMenuContent align="end" className="w-40">
        {menuItems.map((item) => (
          <a key={item.href} href={item.href}>
            <DropdownMenuItem>{item.label}</DropdownMenuItem>
          </a>
        ))}
        {session?.user && (
          <DropdownMenuItem variant="destructive" onClick={handleLogout}>
            Logout
          </DropdownMenuItem>
        )}
        {(sessionError || !session?.user) && (
          <DropdownMenuItem onClick={handleLogin}>Login</DropdownMenuItem>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
