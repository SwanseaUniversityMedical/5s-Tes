import { auth } from "@/lib/auth";
import { headers } from "next/headers";
import { AuthButton } from "@/components/auth-button";

export default async function Home() {
  const session = await auth.api.getSession({ headers: await headers() });

  return (
    <div className="font-sans grid grid-rows-[20px_1fr_20px] items-center justify-items-center min-h-screen p-8 pb-20 gap-16 sm:p-20">
      <h1 className="text-2xl font-bold">Agent Web UI Application</h1>
      You are logged out!
      <AuthButton mode="login" />
    </div>
  );
}
