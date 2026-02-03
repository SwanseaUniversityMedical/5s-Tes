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
import { helpdeskUrl } from "@/lib/constants";
import { getKeycloakIssuer } from "@/lib/helpers";

// User menu items
const MENU_ITEMS = [
  {
    label: "Account",
    href: `${getKeycloakIssuer()}/account`,
  },
  { label: "Helpdesk", href: helpdeskUrl || "#1" },
];

// Creates User Menu Dropdown button Component in the Navbar

export default function UserMenu() {
  const { data: session, error: sessionError } = useSession();
  const username = session?.user?.name;

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
        {MENU_ITEMS.map((item) => (
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
