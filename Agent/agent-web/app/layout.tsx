import type { Metadata } from "next";
import "./globals.css";
import Header from "@/components/layout/Header";
import Footer from "@/components/layout/Footer";
import { Toaster } from "sonner";
import { getSession } from "@/lib/auth-helpers";

export const metadata: Metadata = {
  title: "Agent Web UI Application",
  description: "Agent Web UI Application",
};

export default async function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  const currentSession = await getSession();
  
  return (
    <html lang="en">
      <body
        className={`antialiased`}
      >
        <div className="flex min-h-screen flex-col">
          <Header user={currentSession!.user}/>
          <main className="flex-1">{children}</main>
          <Footer />
        </div>

        <Toaster position="top-center" />
      </body>
    </html>
  );
}
