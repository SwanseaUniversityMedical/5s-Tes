import { AuthButton } from "@/components/auth-button";

export default async function Home() {
  return (
    <div className="font-sans flex flex-col items-center justify-center min-h-screen p-8 pb-20">
      <h1 className="text-2xl font-bold">Agent Web UI Application</h1>
      <p className="my-3 text-gray-500 mt-2">You are logged out!</p>
      <AuthButton mode="login" />
    </div>
  );
}
