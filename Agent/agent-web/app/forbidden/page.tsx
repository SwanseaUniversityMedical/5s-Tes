import { auth } from "@/lib/auth";
import { headers } from "next/headers";
import { AuthButton } from "@/components/auth-button";
import { Metadata } from "next";

interface ForbiddenProps {
  searchParams?: Promise<{ code: string }>;
}
export const metadata: Metadata = {
  title: "Agent Web UI Application",
  description: "Agent Web UI Application",
};

export default async function Forbidden({ searchParams }: ForbiddenProps) {
  const forbiddenCode = (await searchParams)?.code;

  if (forbiddenCode === "403") {
    return (
      <div>
        Forbidden {forbiddenCode}: You don't have permission to access this
        page. Please contact the administrator.
        <AuthButton mode="login" />
      </div>
    );
  } else if (forbiddenCode === "401") {
    return (
      <div>
        Unauthorized {forbiddenCode}: You are not logged in or have been logged
        out. Please login again.
        <AuthButton mode="login" />
      </div>
    );
  } else {
    return <div>Forbidden: You don't have permission to access this page.</div>;
  }
}
