import {MenubarItems} from "@/types/tre-layout";
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
      <ul className="flex items-center gap-6 text-sm font-medium">
        {MENU_NAV_ITEMS.map((item: MenubarItems) => (
          <li key={item.href}>
            <Link
              href={item.href}
              className="
                  relative
                  whitespace-nowrap
                  rounded-md
                  py-1
                  text-foreground
                  transition-all duration-150
                  hover:bg-blue-600/10
                  hover:text-blue-700
                  hover:underline
                  underline-offset-8
              "
            >
              {item.label}
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}