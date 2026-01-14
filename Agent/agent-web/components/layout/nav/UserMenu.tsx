"use client";

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

import { Button } from "@/components/ui/button";
import { ChevronDown, User } from "lucide-react";

// User menu button types

type UserMenuProps = {
  username: string;
  onAccount?: () => void;
  onHelpdesk?: () => void;
  onLogout?: () => void;
};

// Creates User Menu Dropdown button Component in the Navbar

export default function UserMenu({
  username,
  onAccount,
  onHelpdesk,
  onLogout,
}: UserMenuProps) {

  return (
    <DropdownMenu>

    {/* ---- User Menu Button ---- */}
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          className="inline-flex h-8 items-center gap-2 px-3 text-sm font-normal"
        >
          <User className="h-4 w-4 opacity-80" />
            <span
                className="max-w-35px truncate text-foreground">{username}
            </span>
          <ChevronDown className="h-4 w-4 opacity-60" />
        </Button>
      </DropdownMenuTrigger>

    {/* ---- Dropdown Menu Trigger ---- */}
      <DropdownMenuContent align="end" className="w-40">
        <DropdownMenuItem onClick={onAccount}>Account</DropdownMenuItem>
        <DropdownMenuItem onClick={onHelpdesk}>Helpdesk</DropdownMenuItem>
        <DropdownMenuItem
            variant="destructive"
            onClick={onLogout}>
            Logout
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
