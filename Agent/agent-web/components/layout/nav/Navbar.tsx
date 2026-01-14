import MainMenubar from "./MainMenubar";
import NavbarLogo from "./NavbarLogo";
import UserMenu from "./UserMenu";

{
  /* Creates the main navigation bar using the NavbarLogo,
MainMenubar, and UserMenu components. */
}

export default function Navbar() {
  return (
    <nav className="flex items-center justify-between gap-8 bg-background px-6 py-4">
      <div className="flex items-center gap-14">
        <NavbarLogo />
        <MainMenubar />
      </div>
      <UserMenu username="Global AdminUser" />
    </nav>
  );
}
