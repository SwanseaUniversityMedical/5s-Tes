import { Button } from "@/components/ui/button";
import { MenubarItems } from "@/types/tre-layout";
import Link from "next/link";

// Define the menu navigation items with labels and routes

const MENU_NAV_ITEMS = [
  { label: "Projects", href: "/projects" },
  { label: "Configure 5S-TES", href: "/configure-5s-tes" },
  { label: "Access Rules", href: "/access-rules" },
] as const;

// Creates Main Navigation Menubar Component in the Navbar

export default function MainMenubar() {
  return (
    <nav aria-label="Primary navigation" className="hidden md:block">
      <ul className="flex items-center gap-6 font-medium">
        {MENU_NAV_ITEMS.map((item: MenubarItems) => (
          <li key={item.href}>
            <Link href={item.href}>
              <Button
                variant="link"
                className="p-0 font-semibold cursor-pointer text-sm"
              >
                {item.label}
              </Button>
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
