import CredentialsTabs from "@/components/credentials/CredentialsTab";

export default function UpdateCredentials() {
  return (

    <div className="px-20 pt-6 pl-36">
      {/* Page Header */}
      <div className="mb-6">
        <h1 className="text-2xl font-bold">Configure 5S-TES</h1>
        <p className="mt-2 text-gray-600">
          Configure 5S-TES TRE Admin with credentials to access Submission/Egress apps.
        </p>
      </div>

      {/* Credentials Tabs */}
      <CredentialsTabs />
    </div>
  );
}