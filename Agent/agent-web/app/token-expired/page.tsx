"use client";
import { AuthButton } from "@/components/auth-button";
import { signOut } from "@/lib/auth-client";
import { FileWarning, InfoIcon } from "lucide-react";
import { useRouter } from "next/navigation";
import { useEffect } from "react";

export default function TokenExpired() {
  const router = useRouter();
  // sign out the user first, then redirect to the sign-in page
  // TODO: we could only sign out the user only, and by the page content below, ask them to sign in manually + show them how to expand the token's lifespan
  useEffect(() => {
    const signOutAndRedirect = async () => {
      await signOut();
      router.push("/sign-in");
    };
    signOutAndRedirect();
  }, [router]);

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 to-slate-100 flex items-center justify-center p-4">
      <div className="max-w-md w-full">
        <div className="bg-white rounded-2xl shadow-xl p-8 space-y-6">
          {/* Icon */}
          <div className="flex justify-center">
            <FileWarning className="w-10 h-10 text-slate-400" />
          </div>

          {/* Main Content */}
          <div className="text-center space-y-2">
            <h1 className="text-2xl font-bold text-slate-900">
              Session Expired
            </h1>
            <p className="text-slate-600">
              Your access token may have been expired. Please sign in again to
              continue.
            </p>
          </div>

          {/* Action Button */}
          <div className="flex justify-center">
            <AuthButton mode="login" />
          </div>

          <div className="relative">
            <div className="absolute inset-0 flex items-center">
              <div className="w-full border-t border-slate-200"></div>
            </div>
            <div className="relative flex justify-center text-sm">
              <span className="px-4 bg-white text-slate-500 font-medium">
                Tips:
              </span>
            </div>
          </div>

          <div className="bg-slate-50 rounded-lg p-4 space-y-3">
            <div className="flex items-start gap-2">
              <InfoIcon className="w-5 h-5 text-slate-400 mt-0.5 flex-shrink-0" />
              <div className="flex-1">
                <p className="text-sm font-medium text-slate-700 mb-2">
                  Extend token lifespan
                </p>
                <ol className="text-sm text-slate-600 space-y-1.5 list-decimal list-inside marker:text-slate-400">
                  <li>Login to your Keycloak admin console</li>
                  <li>Select the "Dare-TRE" realm</li>
                  <li>Navigate to Realm Settings</li>
                  <li>Open the "Tokens" tab</li>
                  <li>
                    Increase the "Access Token Lifespan" duration under "Access
                    Token" section
                  </li>
                </ol>
              </div>
            </div>
          </div>
        </div>

        {/* Footer */}
        <p className="text-center text-sm text-slate-500 mt-6">
          Having trouble? Contact your administrator
        </p>
      </div>
    </div>
  );
}
