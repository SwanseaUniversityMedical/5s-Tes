import type { Metadata } from "next";
import "./globals.css";
import Header from "@/components/layout/Header";
import Footer from "@/components/layout/Footer";
import { Toaster } from "sonner";

export const metadata: Metadata = {
  title: "Agent Web UI",
  robots: {
    index: false,
    follow: false,
    nocache: true,
    googleBot: {
      index: false,
      follow: false,
      noimageindex: true,
      "max-video-preview": -1,
      "max-image-preview": "none",
      "max-snippet": -1,
    },
  },
  manifest: "/metadata/site.webmanifest",
  icons: {
    icon: [
      {
        url: "/metadata/favicon.ico",
        type: "image/x-icon",
      },
      {
        url: "/metadata/favicon-16x16.png",
        sizes: "16x16",
        type: "image/png",
      },
    ],
    shortcut: [
      {
        url: "/metadata/favicon.ico",
        type: "image/x-icon",
      },
    ],
    apple: [
      {
        url: "/metadata/apple-touch-icon.png",
        sizes: "180x180",
        type: "image/png",
      },
    ],
  },
  description: "Agent Web UI Application",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body className="antialiased">
        <div className="flex min-h-screen flex-col">
          <Header />
          <main className="flex-1">{children}</main>
          <Footer />
        </div>

        <Toaster position="top-center" />
      </body>
    </html>
  );
}
