import Link from "next/link";

// Footer navigation links

const FOOTER_LINKS = [
  { href: "https://docs.federated-analytics.ac.uk/five_safes_tes", label: "About Five Safes TES" },
  { href: "https://dareuk.org.uk/", label: "About DARE UK" },
  { href: "https://dareuk.org.uk/get-in-touch/", label: "Contact Us" },
] as const;


// Creates Footer Component for the Root TRE Layout

export default function Footer() {
  const currentYear = new Date().getFullYear();

  return (
    <footer>

      {/* ---- Bottom Section - Links & Copyright ---- */}

      <div className="bg-[#2e2a6e] py-6 text-white">
        <div className="container mx-auto px-4">

          {/* Navigation Links */}

          <nav
            aria-label="Footer navigation"
            className="mb-4 flex flex-wrap items-center justify-center gap-4 md:gap-6"
          >
            {FOOTER_LINKS.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                target="_blank"
                className="text-sm font-bold text-white transition-colors hover:underline underline-offset-2"
              >
                {link.label}
              </Link>
            ))}
          </nav>

          {/* Copyright */}

          <p className="text-center text-sm text-gray-100">
            ©DARE UK - 5S-TES TRE {currentYear} | All rights reserved
          </p>

        </div>
      </div>
    </footer>
  );
}
