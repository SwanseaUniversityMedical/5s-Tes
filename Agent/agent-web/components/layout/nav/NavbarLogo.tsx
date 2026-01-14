import Link from "next/link";

// Static items for Navigation Logo

const NavLogoItems = {
  appName: "5S-TES",
  adminName: "TRE Admin",
} as const;

// Creates Navigation Logo Component in the Navbar

export default function NavbarLogo () {
  return (
    <Link href="/" aria-label={`${NavLogoItems.appName} ${NavLogoItems.adminName}`}>
      <div className="inline-flex items-center gap-2 whitespace-nowrap text-xl font-semibold">
        <span className="font-semibold tracking-tight">{NavLogoItems.appName}</span>
        <span className="font-normal">|</span>
        <span className="font-normal">{NavLogoItems.adminName}</span>
      </div>
    </Link>
  );
}
