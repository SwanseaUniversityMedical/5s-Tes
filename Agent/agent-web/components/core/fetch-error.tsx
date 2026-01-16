import { AuthButton } from "../auth-button";

export function FetchError({ error }: { error: string }) {
  return (
    <div className="space-y-2">
      <div className="my-5 mx-auto max-w-7xl">
        <div className="flex flex-col items-center justify-center py-20">
          <h2 className="text-xl font-semibold text-red-600">
            Error when loading the page
          </h2>
          <p className="text-sm text-gray-500 mt-2">{error}</p>
          <p className="mt-4 text-blue-600 hover:underline">
            Try logging out and logging in again
          </p>
          <AuthButton mode="logout" />
        </div>
      </div>
    </div>
  );
}
