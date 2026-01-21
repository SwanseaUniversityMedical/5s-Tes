// Define Credential Types

export type CredentialType = "submission" | "egress";

// Define Form Data Structure for Credentials

export type CredentialsFormData = {
  username: string;
  password: string;
  confirmPassword: string;
};

// Define Response Type from Update Credentials API

export type UpdateCredentialsResponse = {
  error?: boolean;
  errorMessage?: string | null;
  id?: number;
  userName: string;
  passwordEnc: string;
  confirmPassword?: string | null;
  credentialType?: number;
  valid?: boolean;
};