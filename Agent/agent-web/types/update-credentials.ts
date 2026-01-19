// Define Credential Types

export type CredentialType = "submission" | "egress";

// Define Form Data Structure for Credentials

export type CredentialsFormData = {
  username: string;
  password: string;
  confirmPassword: string;
};
